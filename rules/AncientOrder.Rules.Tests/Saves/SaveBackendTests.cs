using System.Reflection;
using AncientOrder.Rules.Common;
using AncientOrder.Rules.Saves;
using AncientOrder.Rules.Tests.Support;
using static AncientOrder.Rules.Tests.Support.SaveFixtures;

namespace AncientOrder.Rules.Tests.Saves;

// Carried over from src/persistence/backends.test.ts.
// The memory backend tests keep their names (values are bytes now, not strings).
// The createWebStorageBackend tests become FileSaveBackend tests on a real temporary folder under the same names:
// a failing storage is a file where the folder should be, or a folder where a file should be; the quota cases become the
// disk-full error numbers the backend recognises. Failures surface as SaveStorageException; the store maps those to STORAGE_ERROR.
public class CreateMemoryBackend
{
    private static byte[] Bytes(string text) => System.Text.Encoding.UTF8.GetBytes(text);

    [Fact(DisplayName = "reads null for a missing key, then returns what was written")]
    public async Task Reads_null_then_what_was_written()
    {
        var b = new MemorySaveBackend();
        Assert.Null(await b.ReadAsync("k", Ct));
        await b.WriteAsync("k", Bytes("v"), Ct);
        Assert.Equal(Bytes("v"), await b.ReadAsync("k", Ct));
        Assert.True(await b.ExistsAsync("k", Ct));
    }

    [Fact(DisplayName = "removes keys and ignores removing a missing key")]
    public async Task Removes_keys_and_ignores_missing()
    {
        var b = new MemorySaveBackend();
        await b.WriteAsync("k", Bytes("v"), Ct);
        await b.RemoveAsync("k", Ct);
        await b.RemoveAsync("k", Ct);
        Assert.Null(await b.ReadAsync("k", Ct));
        Assert.False(await b.ExistsAsync("k", Ct));
    }

    [Fact(DisplayName = "keeps separate instances isolated")]
    public async Task Keeps_instances_isolated()
    {
        var a = new MemorySaveBackend();
        var b = new MemorySaveBackend();
        await a.WriteAsync("k", Bytes("v"), Ct);
        Assert.Null(await b.ReadAsync("k", Ct));
    }

    [Fact(DisplayName = "keeps its own copy of what was written and what is read")]
    public async Task Keeps_its_own_copy()
    {
        var b = new MemorySaveBackend();
        var original = Bytes("abc");
        await b.WriteAsync("k", original, Ct);
        original[0] = (byte)'z';
        var read = await b.ReadAsync("k", Ct);
        Assert.Equal(Bytes("abc"), read);
        read![1] = (byte)'z';
        Assert.Equal(Bytes("abc"), await b.ReadAsync("k", Ct));
    }
}

public class CreateWebStorageBackend
{
    private static byte[] Bytes(string text) => System.Text.Encoding.UTF8.GetBytes(text);

    private static async Task<SaveStorageException> Rejection(Func<Task> action)
    {
        var thrown = await Assert.ThrowsAsync<SaveStorageException>(action);
        return thrown;
    }

    private sealed class NumberedIoException : IOException
    {
        public NumberedIoException(int hresult)
            : base("simulated")
        {
            HResult = hresult;
        }
    }

    private static string MappedMessage(Exception thrown)
    {
        var map = typeof(FileSaveBackend).GetMethod("Map", BindingFlags.NonPublic | BindingFlags.Static)!;
        var mapped = (SaveStorageException)map.Invoke(null, [thrown])!;
        Assert.Same(thrown, mapped.InnerException);
        return mapped.Message;
    }

    [Fact(DisplayName = "writes, reads, and removes through the storage")]
    public async Task Writes_reads_and_removes_through_the_folder()
    {
        using var temp = new TempFolder();
        var b = new FileSaveBackend(temp.Path);
        Assert.Null(await b.ReadAsync("manual-01", Ct));
        await b.WriteAsync("manual-01", Bytes("v"), Ct);
        Assert.Equal(Bytes("v"), await File.ReadAllBytesAsync(temp.File("manual-01.json"), Ct));
        Assert.Equal(Bytes("v"), await b.ReadAsync("manual-01", Ct));
        await b.RemoveAsync("manual-01", Ct);
        Assert.False(File.Exists(temp.File("manual-01.json")));
    }

    [Fact(DisplayName = "maps a thrown read to STORAGE_ERROR")]
    public async Task Maps_a_thrown_read()
    {
        using var temp = new TempFolder();
        Directory.CreateDirectory(temp.File("manual-01.json"));
        var b = new FileSaveBackend(temp.Path);
        var err = await Rejection(async () => await b.ReadAsync("manual-01", Ct));
        Assert.Equal(FileSaveBackend.UnavailableMessage, err.Message);
        Assert.NotNull(err.InnerException);
    }

    [Fact(DisplayName = "maps a thrown remove to STORAGE_ERROR")]
    public async Task Maps_a_thrown_remove()
    {
        using var temp = new TempFolder();
        Directory.CreateDirectory(temp.File("manual-01.json"));
        await File.WriteAllTextAsync(System.IO.Path.Combine(temp.File("manual-01.json"), "inner.txt"), "x", Ct);
        var b = new FileSaveBackend(temp.Path);
        var err = await Rejection(async () => await b.RemoveAsync("manual-01", Ct));
        Assert.Equal(FileSaveBackend.UnavailableMessage, err.Message);
    }

    [Fact(DisplayName = "maps a generic write failure to STORAGE_ERROR as unavailable")]
    public async Task Maps_a_generic_write_failure()
    {
        using var temp = new TempFolder();
        await File.WriteAllTextAsync(temp.Path, "a file where the folder should be", Ct);
        try
        {
            var b = new FileSaveBackend(temp.Path);
            var err = await Rejection(async () => await b.WriteAsync("manual-01", Bytes("v"), Ct));
            Assert.Equal("Save storage is unavailable", err.Message);
            Assert.NotNull(err.InnerException);
        }
        finally
        {
            File.Delete(temp.Path);
        }
    }

    [Theory(DisplayName = "maps a quota error to STORAGE_ERROR with the space message")]
    [InlineData(unchecked((int)0x80070070), "ERROR_DISK_FULL")]
    [InlineData(unchecked((int)0x80070027), "ERROR_HANDLE_DISK_FULL")]
    [InlineData(28, "ENOSPC")]
    public void Maps_a_disk_full_error(int hresult, string label)
    {
        Assert.False(string.IsNullOrEmpty(label));
        var message = MappedMessage(new NumberedIoException(hresult));
        Assert.Equal("Not enough storage space to save", message);
        Assert.Equal(FileSaveBackend.DiskFullMessage, message);
    }

    [Fact(DisplayName = "treats a non-object throw as unavailable, not quota")]
    public void Treats_another_failure_as_unavailable()
    {
        Assert.Equal("Save storage is unavailable", MappedMessage(new IOException("some other failure")));
        Assert.Equal("Save storage is unavailable", MappedMessage(new UnauthorizedAccessException("denied")));
        Assert.Equal("Save storage is unavailable", MappedMessage(new NumberedIoException(5)));
    }
}

// New with the file backend: temp-then-replace safety, bounded reads, the missing folder, and the keys it accepts.
public class FileSaveBackendSafety
{
    private static byte[] Bytes(string text) => System.Text.Encoding.UTF8.GetBytes(text);

    [Fact(DisplayName = "treats a missing folder as nothing saved and creates it on the first write")]
    public async Task Missing_folder()
    {
        using var temp = new TempFolder();
        var b = new FileSaveBackend(temp.Path);
        Assert.False(Directory.Exists(temp.Path));
        Assert.Null(await b.ReadAsync("manual-01", Ct));
        Assert.False(await b.ExistsAsync("manual-01", Ct));
        await b.RemoveAsync("manual-01", Ct);

        await b.WriteAsync("manual-01", Bytes("v"), Ct);
        Assert.True(Directory.Exists(temp.Path));
        Assert.Equal(Bytes("v"), await b.ReadAsync("manual-01", Ct));
    }

    [Fact(DisplayName = "leaves only the final file after a write, replacing an older one")]
    public async Task Replaces_leaving_no_temp_file()
    {
        using var temp = new TempFolder();
        var b = new FileSaveBackend(temp.Path);
        await b.WriteAsync("manual-02", Bytes("first"), Ct);
        await b.WriteAsync("manual-02", Bytes("second, longer"), Ct);
        Assert.Equal(["manual-02.json"], temp.FileNames());
        Assert.Equal(Bytes("second, longer"), await b.ReadAsync("manual-02", Ct));
    }

    [Fact(DisplayName = "removing a key deletes only that file")]
    public async Task Remove_deletes_only_that_file()
    {
        using var temp = new TempFolder();
        var b = new FileSaveBackend(temp.Path);
        await b.WriteAsync("manual-01", Bytes("a"), Ct);
        await b.WriteAsync("manual-02", Bytes("b"), Ct);
        await b.RemoveAsync("manual-01", Ct);
        Assert.Equal(["manual-02.json"], temp.FileNames());
        Assert.False(await b.ExistsAsync("manual-01", Ct));
        Assert.True(await b.ExistsAsync("manual-02", Ct));
    }

    [Fact(DisplayName = "reads at most the save size limit plus one byte of an oversized file")]
    public async Task Bounded_read_of_an_oversized_file()
    {
        using var temp = new TempFolder();
        Directory.CreateDirectory(temp.Path);
        await File.WriteAllBytesAsync(temp.File("manual-01.json"), new byte[5_000_000], Ct);
        var b = new FileSaveBackend(temp.Path);
        var read = await b.ReadAsync("manual-01", Ct);
        Assert.NotNull(read);
        Assert.Equal(SaveConstants.MaxSaveBytes + 1, read.Length);

        Directory.CreateDirectory(temp.Path);
        await File.WriteAllBytesAsync(temp.File("manual-02.json"), new byte[SaveConstants.MaxSaveBytes], Ct);
        Assert.Equal(SaveConstants.MaxSaveBytes, (await b.ReadAsync("manual-02", Ct))!.Length);
    }

    [Fact(DisplayName = "a failed write leaves the old save intact and removes its temp file")]
    public async Task Failed_write_keeps_the_old_save()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Holding a file open so the replace fails is a Windows behaviour.");
        using var temp = new TempFolder();
        var b = new FileSaveBackend(temp.Path);
        await b.WriteAsync("manual-01", Bytes("old save"), Ct);

        await using (new FileStream(temp.File("manual-01.json"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAsync<SaveStorageException>(async () => await b.WriteAsync("manual-01", Bytes("new save"), Ct));
        }

        Assert.Equal(["manual-01.json"], temp.FileNames());
        Assert.Equal(Bytes("old save"), await b.ReadAsync("manual-01", Ct));
    }

    [Fact(DisplayName = "a write that cannot create its temp file leaves the old save intact")]
    public async Task Blocked_temp_file_keeps_the_old_save()
    {
        using var temp = new TempFolder();
        var b = new FileSaveBackend(temp.Path);
        await b.WriteAsync("manual-01", Bytes("old save"), Ct);
        Directory.CreateDirectory(temp.File("manual-01.json.tmp"));

        await Assert.ThrowsAsync<SaveStorageException>(async () => await b.WriteAsync("manual-01", Bytes("new save"), Ct));
        Assert.Equal(Bytes("old save"), await b.ReadAsync("manual-01", Ct));
    }

    [Fact(DisplayName = "a crash after the temp file is written leaves the old save loadable, and the next save replaces the leftover")]
    public async Task Crash_after_the_temp_file()
    {
        using var temp = new TempFolder();
        var content = SaveFixtures.Content;
        var files = new FileSaveBackend(temp.Path);
        var game = MakeGame("Old");
        Assert.True((await new SaveStore(files, content).SaveAsync(1, game, 1, Ct)).IsOk);

        var crashing = new CrashAfterTempFile(files, temp.Path);
        var failed = await new SaveStore(crashing, content).SaveAsync(1, MakeGame("New"), 2, Ct);
        Assert.False(failed.IsOk);
        Assert.Equal(ErrorCode.STORAGE_ERROR, failed.Error.Code);
        Assert.Contains("manual-01.json.tmp", temp.FileNames());

        var restarted = new SaveStore(new FileSaveBackend(temp.Path), content);
        var loaded = await restarted.LoadAsync(1, Ct);
        Assert.True(loaded.IsOk, loaded.IsOk ? string.Empty : loaded.Error.Message);
        Assert.Equal("Old", loaded.Value.Game.Player.Name);
        var listed = (await restarted.ListAsync(Ct)).Value;
        Assert.Equal(SlotStatus.Saved, listed[0].Status);
        Assert.All(listed.Skip(1), s => Assert.Equal(SlotStatus.Empty, s.Status));

        Assert.True((await restarted.SaveAsync(1, MakeGame("Newest"), 3, Ct)).IsOk);
        Assert.Equal(["manual-01.json"], temp.FileNames());
        Assert.Equal("Newest", (await restarted.LoadAsync(1, Ct)).Value.Game.Player.Name);
    }

    [Theory(DisplayName = "refuses keys that could leave the folder")]
    [InlineData("../manual-01")]
    [InlineData("manual-01/../../x")]
    [InlineData("MANUAL-01")]
    [InlineData("")]
    [InlineData("manual 01")]
    public async Task Refuses_unsafe_keys(string key)
    {
        using var temp = new TempFolder();
        var b = new FileSaveBackend(temp.Path);
        await Assert.ThrowsAsync<ArgumentException>(async () => await b.ReadAsync(key, Ct));
        await Assert.ThrowsAsync<ArgumentException>(async () => await b.WriteAsync(key, Bytes("x"), Ct));
        Assert.False(Directory.Exists(temp.Path));
    }

    [Fact(DisplayName = "maps an unreadable folder to the unavailable message")]
    public async Task Unavailable_through_the_store()
    {
        using var temp = new TempFolder();
        Directory.CreateDirectory(temp.File("manual-03.json"));
        var store = new SaveStore(new FileSaveBackend(temp.Path), SaveFixtures.Content);
        var load = await store.LoadAsync(3, Ct);
        Assert.Equal(ErrorCode.STORAGE_ERROR, Refused(load, ErrorCode.STORAGE_ERROR).Code);
        Assert.Equal("Save storage is unavailable", load.Error.Message);
        var list = await store.ListAsync(Ct);
        Assert.Equal("Save storage is unavailable", Refused(list, ErrorCode.STORAGE_ERROR).Message);
    }

    // Writes the new bytes to the temp file as the file backend would, then fails before the replace.
    private sealed class CrashAfterTempFile(ISaveBackend inner, string folder) : ISaveBackend
    {
        public ValueTask<byte[]?> ReadAsync(string key, CancellationToken cancellationToken = default) => inner.ReadAsync(key, cancellationToken);

        public ValueTask<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) => inner.ExistsAsync(key, cancellationToken);

        public ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default) => inner.RemoveAsync(key, cancellationToken);

        public async ValueTask WriteAsync(string key, byte[] bytes, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(folder);
            await File.WriteAllBytesAsync(System.IO.Path.Combine(folder, key + ".json.tmp"), bytes, cancellationToken);
            throw new SaveStorageException("Save storage is unavailable");
        }
    }
}
