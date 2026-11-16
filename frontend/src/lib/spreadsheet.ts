/**
 * ĐỌC / GHI BẢNG TÍNH ngay trên trình duyệt — không cần thư viện ngoài.
 *  - Đọc: .xlsx (giải nén bằng DecompressionStream có sẵn trong trình duyệt), .csv / .txt (tự nhận dấu phân cách , ; tab)
 *  - Ghi: .xlsx tối giản (1 sheet, chữ thuần) — dùng cho file mẫu và file "dòng lỗi"
 * Không hỗ trợ .xls (Excel 97–2003): người dùng "Lưu thành .xlsx" trước.
 * Khi có backend: có thể để server đọc file, nhưng giữ kiểu trả về `string[][]`.
 */

export type Sheet = string[][];

// ============================== ĐỌC ==============================

export async function readSpreadsheet(file: File): Promise<Sheet> {
  const ext = file.name.split('.').pop()?.toLowerCase();
  if (ext === 'xls') throw new Error('Không đọc được file .xls (Excel 97–2003). Mở file trong Excel → Lưu thành .xlsx rồi chọn lại.');
  if (ext === 'xlsx') return readXlsx(await file.arrayBuffer());
  if (ext === 'csv' || ext === 'txt' || ext === 'tsv') return parseCsv(await file.text());
  throw new Error('Chỉ nhận file .xlsx hoặc .csv');
}

/** CSV theo RFC 4180: dấu nháy kép, xuống dòng trong ô. Tự đoán dấu phân cách từ dòng đầu. */
export function parseCsv(text: string): Sheet {
  const src = text.replace(/^﻿/, '');
  const firstLine = src.slice(0, src.indexOf('\n') >>> 0);
  const delim = [',', ';', '\t'].map((d) => ({ d, n: firstLine.split(d).length })).sort((a, b) => b.n - a.n)[0].d;
  const rows: Sheet = [];
  let row: string[] = [];
  let cell = '';
  let quoted = false;
  for (let i = 0; i < src.length; i++) {
    const ch = src[i];
    if (quoted) {
      if (ch === '"' && src[i + 1] === '"') {
        cell += '"';
        i++;
      } else if (ch === '"') quoted = false;
      else cell += ch;
    } else if (ch === '"' && cell === '') quoted = true;
    else if (ch === delim) {
      row.push(cell);
      cell = '';
    } else if (ch === '\n' || ch === '\r') {
      if (ch === '\r' && src[i + 1] === '\n') i++;
      row.push(cell);
      rows.push(row);
      row = [];
      cell = '';
    } else cell += ch;
  }
  if (cell !== '' || row.length) {
    row.push(cell);
    rows.push(row);
  }
  return rows;
}

// ---- Giải nén zip (xlsx là file zip chứa các file XML) ----

async function unzip(buf: ArrayBuffer, wanted: (name: string) => boolean): Promise<Map<string, string>> {
  const view = new DataView(buf);
  const bytes = new Uint8Array(buf);
  // Tìm "End of central directory" từ cuối file
  let eocd = -1;
  for (let i = buf.byteLength - 22; i >= Math.max(0, buf.byteLength - 65_557); i--) {
    if (view.getUint32(i, true) === 0x06054b50) {
      eocd = i;
      break;
    }
  }
  if (eocd < 0) throw new Error('File không phải .xlsx hợp lệ (có thể bị hỏng hoặc đang được đặt mật khẩu).');
  const count = view.getUint16(eocd + 10, true);
  let p = view.getUint32(eocd + 16, true);
  const dec = new TextDecoder();
  const out = new Map<string, string>();
  for (let n = 0; n < count; n++) {
    if (view.getUint32(p, true) !== 0x02014b50) break;
    const method = view.getUint16(p + 10, true);
    const size = view.getUint32(p + 20, true);
    const nameLen = view.getUint16(p + 28, true);
    const extraLen = view.getUint16(p + 30, true);
    const commentLen = view.getUint16(p + 32, true);
    const local = view.getUint32(p + 42, true);
    const name = dec.decode(bytes.subarray(p + 46, p + 46 + nameLen));
    p += 46 + nameLen + extraLen + commentLen;
    if (!wanted(name)) continue;
    const start = local + 30 + view.getUint16(local + 26, true) + view.getUint16(local + 28, true);
    const data = bytes.slice(start, start + size);
    if (method === 0) out.set(name, dec.decode(data));
    else if (method === 8) {
      const stream = new Blob([data]).stream().pipeThrough(new DecompressionStream('deflate-raw'));
      out.set(name, await new Response(stream).text());
    }
  }
  return out;
}

const xml = (text?: string) => (text ? new DOMParser().parseFromString(text, 'application/xml') : undefined);
const tags = (root: Document | Element, name: string) => Array.from(root.getElementsByTagNameNS('*', name));

/** Chữ trong 1 ô / chuỗi dùng chung — bỏ phần phiên âm furigana (<rPh>) Excel tự thêm cho tiếng Nhật */
function textOf(el: Element): string {
  return tags(el, 't')
    .filter((t) => !(t.parentElement?.localName === 'rPh' || t.parentElement?.parentElement?.localName === 'rPh'))
    .map((t) => t.textContent ?? '')
    .join('');
}

/** "AB12" → 27 (cột, đếm từ 0) */
function colIndex(ref: string): number {
  let n = 0;
  for (const ch of ref.replace(/\d+$/, '')) n = n * 26 + (ch.charCodeAt(0) - 64);
  return n - 1;
}

async function readXlsx(buf: ArrayBuffer): Promise<Sheet> {
  const files = await unzip(buf, (n) => n === 'xl/workbook.xml' || n === 'xl/_rels/workbook.xml.rels' || n === 'xl/sharedStrings.xml' || n.startsWith('xl/worksheets/'));

  // Sheet đầu tiên theo thứ tự trong workbook
  let sheetPath = 'xl/worksheets/sheet1.xml';
  const wb = xml(files.get('xl/workbook.xml'));
  const rels = xml(files.get('xl/_rels/workbook.xml.rels'));
  const firstSheet = wb && tags(wb, 'sheet')[0];
  const rid = firstSheet?.getAttribute('r:id') ?? firstSheet?.getAttributeNS('http://schemas.openxmlformats.org/officeDocument/2006/relationships', 'id');
  const target = rels && rid ? tags(rels, 'Relationship').find((r) => r.getAttribute('Id') === rid)?.getAttribute('Target') : undefined;
  if (target) sheetPath = target.startsWith('/') ? target.slice(1) : `xl/${target}`;

  const sheet = xml(files.get(sheetPath));
  if (!sheet) throw new Error('Không tìm thấy sheet dữ liệu trong file.');
  const sst = xml(files.get('xl/sharedStrings.xml'));
  const shared = sst ? tags(sst, 'si').map(textOf) : [];

  const out: Sheet = [];
  for (const row of tags(sheet, 'row')) {
    const r = Number(row.getAttribute('r') ?? out.length + 1) - 1;
    const cells: string[] = [];
    tags(row, 'c').forEach((c, i) => {
      const ref = c.getAttribute('r');
      const col = ref ? colIndex(ref) : i;
      const type = c.getAttribute('t');
      const v = tags(c, 'v')[0]?.textContent ?? '';
      cells[col] = type === 's' ? (shared[Number(v)] ?? '') : type === 'inlineStr' ? textOf(c) : type === 'b' ? (v === '1' ? 'TRUE' : 'FALSE') : v;
    });
    out[r] = Array.from(cells, (x) => x ?? '');
  }
  return Array.from(out, (x) => x ?? []);
}

// ============================== GHI ==============================

const CRC_TABLE = (() => {
  const t = new Uint32Array(256);
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    t[n] = c >>> 0;
  }
  return t;
})();
function crc32(data: Uint8Array): number {
  let c = 0xffffffff;
  for (const b of data) c = CRC_TABLE[(c ^ b) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

/** Zip không nén (stored) — đủ cho file vài trăm KB */
function zip(entries: { name: string; text: string }[]): Blob {
  const enc = new TextEncoder();
  const parts: Uint8Array[] = [];
  const central: Uint8Array[] = [];
  let offset = 0;
  for (const e of entries) {
    const name = enc.encode(e.name);
    const data = enc.encode(e.text);
    const crc = crc32(data);
    const local = new DataView(new ArrayBuffer(30));
    local.setUint32(0, 0x04034b50, true);
    local.setUint16(4, 20, true);
    local.setUint16(6, 0x0800, true); // tên file UTF-8
    local.setUint32(14, crc, true);
    local.setUint32(18, data.length, true);
    local.setUint32(22, data.length, true);
    local.setUint16(26, name.length, true);
    parts.push(new Uint8Array(local.buffer), name, data);

    const cd = new DataView(new ArrayBuffer(46));
    cd.setUint32(0, 0x02014b50, true);
    cd.setUint16(4, 20, true);
    cd.setUint16(6, 20, true);
    cd.setUint16(8, 0x0800, true);
    cd.setUint32(16, crc, true);
    cd.setUint32(20, data.length, true);
    cd.setUint32(24, data.length, true);
    cd.setUint16(28, name.length, true);
    cd.setUint32(42, offset, true);
    central.push(new Uint8Array(cd.buffer), name);
    offset += 30 + name.length + data.length;
  }
  const cdSize = central.reduce((t, b) => t + b.length, 0);
  const end = new DataView(new ArrayBuffer(22));
  end.setUint32(0, 0x06054b50, true);
  end.setUint16(8, entries.length, true);
  end.setUint16(10, entries.length, true);
  end.setUint32(12, cdSize, true);
  end.setUint32(16, offset, true);
  return new Blob([...parts, ...central, new Uint8Array(end.buffer)] as BlobPart[], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
}

const esc = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/[\u0000-\u0008\u000b\u000c\u000e-\u001f]/g, '');
const colName = (i: number): string => (i < 26 ? String.fromCharCode(65 + i) : colName(Math.floor(i / 26) - 1) + String.fromCharCode(65 + (i % 26)));

/**
 * Tạo file .xlsx 1 sheet. Mọi ô ghi dạng CHỮ (giữ số 0 đầu SĐT). Dòng đầu in đậm + cố định khi cuộn.
 * `widths`: độ rộng cột (ký tự).
 */
export function buildXlsx(rows: Sheet, opts: { sheetName?: string; widths?: number[] } = {}): Blob {
  const sheetName = esc((opts.sheetName ?? 'Sheet1').slice(0, 31));
  const cols = opts.widths?.length ? `<cols>${opts.widths.map((w, i) => `<col min="${i + 1}" max="${i + 1}" width="${w}" customWidth="1"/>`).join('')}</cols>` : '';
  const body = rows
    .map((r, ri) => `<row r="${ri + 1}">${r.map((v, ci) => `<c r="${colName(ci)}${ri + 1}" t="inlineStr"${ri === 0 ? ' s="1"' : ''}><is><t xml:space="preserve">${esc(String(v ?? ''))}</t></is></c>`).join('')}</row>`)
    .join('');
  const NS = 'http://schemas.openxmlformats.org';
  return zip([
    {
      name: '[Content_Types].xml',
      text: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="${NS}/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>`,
    },
    {
      name: '_rels/.rels',
      text: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="${NS}/package/2006/relationships"><Relationship Id="rId1" Type="${NS}/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>`,
    },
    {
      name: 'xl/workbook.xml',
      text: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="${NS}/spreadsheetml/2006/main" xmlns:r="${NS}/officeDocument/2006/relationships"><sheets><sheet name="${sheetName}" sheetId="1" r:id="rId1"/></sheets></workbook>`,
    },
    {
      name: 'xl/_rels/workbook.xml.rels',
      text: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="${NS}/package/2006/relationships"><Relationship Id="rId1" Type="${NS}/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="${NS}/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>`,
    },
    {
      name: 'xl/styles.xml',
      text: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="${NS}/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border/></borders><cellStyleXfs count="1"><xf/></cellStyleXfs><cellXfs count="2"><xf fontId="0" numFmtId="49" applyNumberFormat="1"/><xf fontId="1" numFmtId="49" applyFont="1" applyNumberFormat="1"/></cellXfs></styleSheet>`,
    },
    {
      name: 'xl/worksheets/sheet1.xml',
      text: `<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="${NS}/spreadsheetml/2006/main"><sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>${cols}<sheetData>${body}</sheetData></worksheet>`,
    },
  ]);
}

/** Tải file về máy */
export function downloadBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = filename;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

// ============================== TIỆN ÍCH CHO Ô ==============================

/**
 * Ô ngày → "YYYY-MM-DD". Nhận: 15/03/2008, 15-03-2008, 2008-03-15, hoặc số seri ngày của Excel (39522).
 * Trả về undefined nếu ô trống, null nếu sai định dạng.
 */
export function parseDateCell(raw: string): string | undefined | null {
  const v = raw.trim();
  if (!v) return undefined;
  if (/^\d{4,5}(\.\d+)?$/.test(v)) {
    const d = new Date(Math.round((Number(v) - 25569) * 86_400_000));
    return Number.isNaN(d.getTime()) ? null : d.toISOString().slice(0, 10);
  }
  let m = v.match(/^(\d{1,2})[/.-](\d{1,2})[/.-](\d{4})$/);
  if (m) return validYmd(Number(m[3]), Number(m[2]), Number(m[1]));
  m = v.match(/^(\d{4})[/.-](\d{1,2})[/.-](\d{1,2})/);
  if (m) return validYmd(Number(m[1]), Number(m[2]), Number(m[3]));
  return null;
}
function validYmd(y: number, mo: number, d: number): string | null {
  const dt = new Date(Date.UTC(y, mo - 1, d));
  if (dt.getUTCFullYear() !== y || dt.getUTCMonth() !== mo - 1 || dt.getUTCDate() !== d) return null;
  return dt.toISOString().slice(0, 10);
}

/** SĐT Việt Nam: bỏ ký tự thừa, +84 → 0, Excel làm mất số 0 đầu (912345678 → 0912345678) */
export function normalizePhone(raw: string): string {
  let d = raw.replace(/\D/g, '');
  if (d.startsWith('84') && d.length === 11) d = `0${d.slice(2)}`;
  if (d.length === 9 && !d.startsWith('0')) d = `0${d}`;
  return d;
}
export const isValidPhone = (p: string) => /^0\d{9}$/.test(p);
export const isValidEmail = (e: string) => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(e);

/** Ô chọn từ danh sách: khớp theo mã hoặc nhãn, không phân biệt dấu / hoa thường ("facebook", "Facebook", "Giới thiệu") */
export function matchOption<V extends string>(raw: string, options: { value: V; label: string; aliases?: string[] }[]): V | undefined {
  const key = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase().replace(/[^a-z0-9]/g, '');
  const k = key(raw);
  if (!k) return undefined;
  return options.find((o) => [o.value, o.label, ...(o.aliases ?? [])].some((x) => key(x) === k))?.value;
}
