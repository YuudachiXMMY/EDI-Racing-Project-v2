import { useState } from 'react';
import { buildShareUrl } from '../gameLaunch.js';

export default function SharePanel({ shareCode, isActive, onToggleActive }) {
  const [copied, setCopied] = useState(false);

  const shareUrl = buildShareUrl(shareCode);

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(shareUrl);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Fallback: select the input text
      const input = document.querySelector('.share-url');
      if (input) {
        input.select();
        document.execCommand('copy');
        setCopied(true);
        setTimeout(() => setCopied(false), 2000);
      }
    }
  }

  return (
    <div className="share-panel">
      <div className="share-row">
        <span className="share-label">Survey Link:</span>
        <input
          type="text"
          className="share-url"
          value={shareUrl}
          readOnly
          onClick={e => e.target.select()}
        />
        <button onClick={handleCopy} className="btn-secondary btn-small copy-btn">
          {copied ? '已复制!' : '复制链接'}
        </button>
        <button
          onClick={() => onToggleActive(!isActive)}
          className={`btn-small active-toggle ${isActive ? 'active' : 'inactive'}`}
        >
          {isActive ? '启用' : '停用'}
        </button>
      </div>
    </div>
  );
}
