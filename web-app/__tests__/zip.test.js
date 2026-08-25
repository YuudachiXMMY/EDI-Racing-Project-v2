import { describe, it, expect } from 'vitest';
import { createZip } from '../src/lib/zip.js';

// Minimal in-memory reader for STORE-method zips (no compression, no comment).
// Parses the End-of-Central-Directory + central directory, then extracts each
// entry's stored bytes by walking its local header. Keeps the test pure — no
// filesystem or external `unzip` — while genuinely round-tripping the archive.
function readZip(buf) {
  // EOCD is the final 22 bytes when there is no archive comment.
  const eocd = buf.length - 22;
  expect(buf.readUInt32LE(eocd)).toBe(0x06054b50);
  const count = buf.readUInt16LE(eocd + 10);
  let ptr = buf.readUInt32LE(eocd + 16); // central directory offset

  const files = {};
  for (let i = 0; i < count; i++) {
    expect(buf.readUInt32LE(ptr)).toBe(0x02014b50);
    const compression = buf.readUInt16LE(ptr + 10);
    const size = buf.readUInt32LE(ptr + 24);
    const nameLen = buf.readUInt16LE(ptr + 28);
    const extraLen = buf.readUInt16LE(ptr + 30);
    const commentLen = buf.readUInt16LE(ptr + 32);
    const localOffset = buf.readUInt32LE(ptr + 42);
    const name = buf.toString('utf8', ptr + 46, ptr + 46 + nameLen);

    expect(compression).toBe(0); // store only
    expect(buf.readUInt32LE(localOffset)).toBe(0x04034b50);
    const localNameLen = buf.readUInt16LE(localOffset + 26);
    const localExtraLen = buf.readUInt16LE(localOffset + 28);
    const dataStart = localOffset + 30 + localNameLen + localExtraLen;
    files[name] = buf.subarray(dataStart, dataStart + size);

    ptr += 46 + nameLen + extraLen + commentLen;
  }
  return files;
}

describe('createZip', () => {
  it('produces a valid archive that round-trips string and buffer entries', () => {
    const zip = createZip([
      { name: 'hello.txt', data: 'Hello, world!' },
      { name: 'data.csv', data: 'a,b\n1,2\n' },
      { name: 'blob.bin', data: Buffer.from([0, 1, 2, 255, 254]) },
    ]);

    // Standard local-file-header signature at the very start ("PK\x03\x04").
    expect(zip.readUInt32LE(0)).toBe(0x04034b50);

    const files = readZip(zip);
    expect(Object.keys(files).sort()).toEqual(['blob.bin', 'data.csv', 'hello.txt']);
    expect(files['hello.txt'].toString('utf8')).toBe('Hello, world!');
    expect(files['data.csv'].toString('utf8')).toBe('a,b\n1,2\n');
    expect([...files['blob.bin']]).toEqual([0, 1, 2, 255, 254]);
  });

  it('preserves UTF-8 content and reports the correct entry count', () => {
    const zip = createZip([{ name: 'notes.txt', data: 'café — naïve — 日本語' }]);
    const files = readZip(zip);
    expect(Object.keys(files)).toHaveLength(1);
    expect(files['notes.txt'].toString('utf8')).toBe('café — naïve — 日本語');
  });

  it('handles an empty file entry', () => {
    const zip = createZip([{ name: 'empty.txt', data: '' }]);
    const files = readZip(zip);
    expect(files['empty.txt'].length).toBe(0);
  });
});
