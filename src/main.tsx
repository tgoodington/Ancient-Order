import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './ui/App';
import { createAppSession } from './ui/appSession';
import './ui/styles/reset.css';
import './ui/styles/tokens.css';

const session = createAppSession();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App session={session} />
  </StrictMode>
);
