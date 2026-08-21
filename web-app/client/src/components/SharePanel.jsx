import { useState, useEffect } from 'react';
import QRCode from 'qrcode';
import { buildShareUrl } from '../gameLaunch.js';

export default function SharePanel({ shareCode, isActive, onToggleActive }) {
  const [copied, setCopied] = useState(false);
  const [showQr, setShowQr] = useState(false);
  const [qrDataUrl, setQrDataUrl] = useState('');

  const shareUrl = buildShareUrl(shareCode);

  // Render the QR only while the modal is open. A larger size keeps it scannable when the
  // professor projects it for the class. Regenerate whenever the link changes.
  useEffect(() => {
    if (!showQr || !shareUrl) { setQrDataUrl(''); return; }
    let cancelled = false;
    QRCode.toDataURL(shareUrl, { width: 320, margin: 1 })
      .then(url => { if (!cancelled) setQrDataUrl(url); })
      .catch(() => { if (!cancelled) setQrDataUrl(''); });
    return () => { cancelled = true; };
  }, [showQr, shareUrl]);

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
          {copied ? 'Copied!' : 'Copy Link'}
        </button>
        <button onClick={() => setShowQr(true)} className="btn-secondary btn-small">
          Show QR
        </button>
        <button
          onClick={() => onToggleActive(!isActive)}
          className={`btn-small active-toggle ${isActive ? 'active' : 'inactive'}`}
        >
          {isActive ? 'Active' : 'Inactive'}
        </button>
      </div>

      {showQr && (
        <div className="host-room-overlay" onClick={() => setShowQr(false)}>
          <div className="host-room-panel" onClick={e => e.stopPropagation()}>
            <button className="host-room-close" onClick={() => setShowQr(false)} aria-label="Close">×</button>
            <h2>Survey Link</h2>
            <div className="host-room-ready">
              {qrDataUrl && (
                <img className="host-room-qr" src={qrDataUrl} alt="QR code for the survey link" width={320} height={320} />
              )}
              <p className="host-room-hint">Students scan the QR code or open this link:</p>
              <div className="host-room-link-row">
                <input
                  type="text"
                  className="host-room-url"
                  value={shareUrl}
                  readOnly
                  onClick={e => e.target.select()}
                />
                <button onClick={handleCopy} className="btn-secondary btn-small">
                  {copied ? 'Copied!' : 'Copy Link'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
