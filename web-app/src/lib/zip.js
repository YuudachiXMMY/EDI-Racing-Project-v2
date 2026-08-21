/**
 * Minimal, dependency-free ZIP writer (STORE method — no compression).
 *
 * The export bundle packs a handful of small files (a workbook + two CSVs), so
 * compression buys nothing and a pure-JS store-only writer avoids pulling in a
 * zip dependency. Output is a standard .zip readable by any archiver.
 *
 * Only the subset of the ZIP spec needed for stored entries is implemented:
 * local file headers, the central directory, and the end-of-central-directory
 * record. No ZIP64, no encryption, no data descriptors.
 */

// Precomputed CRC-32 lookup table (IEEE 802.3 polynomial, reflected 0xEDB88320).
const CRC_TABLE = (() => {
  const table = new Int32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) {
      c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    }
    table[n] = c;
  }
  return table;
})();

/** CRC-32 checksum of a Buffer, returned as an unsigned 32-bit integer. */
function crc32(buf) {
  let crc = -1;
  for (let i = 0; i < buf.length; i++) {
    crc = (crc >>> 8) ^ CRC_TABLE[(crc ^ buf[i]) & 0xff];
  }
  return (crc ^ -1) >>> 0;
}

// Fixed DOS timestamp (1980-01-01 00:00:00). A constant keeps the output
// deterministic — the on-disk file dates are irrelevant to consumers here.
const DOS_TIME = 0;
const DOS_DATE = 0x0021; // (year 1980)<<9 | (month 1)<<5 | day 1

/**
 * Build a ZIP archive from a list of files.
 *
 * @param {Array<{name: string, data: Buffer|string}>} files
 * @returns {Buffer} the complete .zip archive
 */
export function createZip(files) {
  const entries = files.map(f => {
    const nameBuf = Buffer.from(f.name, 'utf8');
    const data = Buffer.isBuffer(f.data) ? f.data : Buffer.from(f.data, 'utf8');
    return { nameBuf, data, crc: crc32(data) };
  });

  const localParts = [];
  const centralParts = [];
  let offset = 0;

  for (const e of entries) {
    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0); // local file header signature
    local.writeUInt16LE(20, 4);         // version needed to extract
    local.writeUInt16LE(0, 6);          // general purpose flags
    local.writeUInt16LE(0, 8);          // compression method = store
    local.writeUInt16LE(DOS_TIME, 10);
    local.writeUInt16LE(DOS_DATE, 12);
    local.writeUInt32LE(e.crc, 14);
    local.writeUInt32LE(e.data.length, 18); // compressed size
    local.writeUInt32LE(e.data.length, 22); // uncompressed size
    local.writeUInt16LE(e.nameBuf.length, 26);
    local.writeUInt16LE(0, 28);         // extra field length

    localParts.push(local, e.nameBuf, e.data);

    const central = Buffer.alloc(46);
    central.writeUInt32LE(0x02014b50, 0); // central directory header signature
    central.writeUInt16LE(20, 4);         // version made by
    central.writeUInt16LE(20, 6);         // version needed to extract
    central.writeUInt16LE(0, 8);          // general purpose flags
    central.writeUInt16LE(0, 10);         // compression method = store
    central.writeUInt16LE(DOS_TIME, 12);
    central.writeUInt16LE(DOS_DATE, 14);
    central.writeUInt32LE(e.crc, 16);
    central.writeUInt32LE(e.data.length, 20);
    central.writeUInt32LE(e.data.length, 24);
    central.writeUInt16LE(e.nameBuf.length, 28);
    central.writeUInt16LE(0, 30);         // extra field length
    central.writeUInt16LE(0, 32);         // file comment length
    central.writeUInt16LE(0, 34);         // disk number start
    central.writeUInt16LE(0, 36);         // internal file attributes
    central.writeUInt32LE(0, 38);         // external file attributes
    central.writeUInt32LE(offset, 42);    // relative offset of local header

    centralParts.push(central, e.nameBuf);

    offset += local.length + e.nameBuf.length + e.data.length;
  }

  const centralDir = Buffer.concat(centralParts);
  const centralSize = centralDir.length;
  const centralOffset = offset;

  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);   // end of central directory signature
  end.writeUInt16LE(0, 4);            // number of this disk
  end.writeUInt16LE(0, 6);            // disk with start of central directory
  end.writeUInt16LE(entries.length, 8);  // central dir records on this disk
  end.writeUInt16LE(entries.length, 10); // total central dir records
  end.writeUInt32LE(centralSize, 12);
  end.writeUInt32LE(centralOffset, 16);
  end.writeUInt16LE(0, 20);          // ZIP file comment length

  return Buffer.concat([...localParts, centralDir, end]);
}
