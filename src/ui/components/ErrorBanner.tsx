import styles from './ErrorBanner.module.css';

interface ErrorBannerProps {
  message: string;
  onDismiss: () => void;
}

export function ErrorBanner({ message, onDismiss }: ErrorBannerProps) {
  return (
    <div role="alert" className={styles.banner}>
      <span className={styles.message}>{message}</span>
      <button
        className={styles.dismissButton}
        onClick={onDismiss}
        aria-label="Dismiss error"
      >
        &times;
      </button>
    </div>
  );
}
