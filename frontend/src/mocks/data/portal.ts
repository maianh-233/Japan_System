import dayjs from 'dayjs';
import type { CancelRequest, CardProgress, FlashDeck, LeaveRequest, SubInvite, TeacherDoc } from '@/features/portal/types';
import { MOCK_SESSIONS } from './attendance';
import { MOCK_CLASSES } from './classes';
import { MOCK_ENROLLMENTS } from './students';

/**
 * Dữ liệu giả — Cổng GV / HV / PH: đơn hủy buổi, lời mời dạy thay, tài liệu GV, đơn xin nghỉ, flashcard.
 * Tài khoản demo: GV Nguyễn Thu Hà (t-ha) · HV Nguyễn Văn An (ST-1) · PH Nguyễn Văn Bình (con ST-1, ST-2).
 */

const ago = (min: number) => dayjs().subtract(min, 'minute').toISOString();
const today = dayjs().format('YYYY-MM-DD');
const future = (classId: string, k = 0) => MOCK_SESSIONS.filter((s) => s.classId === classId && s.status === 'scheduled' && s.date > today)[k];
const past = (classId: string, k = 0) => MOCK_SESSIONS.filter((s) => s.classId === classId && s.date < today && s.status !== 'cancelled').slice(-1 - k)[0];

// ---------------- Đơn hủy buổi của GV ----------------

const req: CancelRequest[] = [];
const f1 = future('C-N3K12', 2);
if (f1) req.push({ id: 'CR-1', sessionId: f1.id, classId: f1.classId, teacherId: 't-ha', reasonType: 'Việc gia đình đột xuất', reason: 'Đưa bố đi khám định kỳ ở bệnh viện, không kịp về dạy ca tối.', makeupAt: dayjs(f1.date).add(1, 'day').hour(18).minute(0).toISOString(), status: 'pending', createdAt: ago(90) });
const p1 = past('C-N2K05', 3);
if (p1) req.push({ id: 'CR-2', sessionId: p1.id, classId: p1.classId, teacherId: 't-ha', reasonType: 'Ốm / sức khỏe', reason: 'Sốt cao, có giấy khám bệnh.', status: 'approved', createdAt: ago(60 * 24 * 12), decidedBy: 'Vũ Ngọc Mai', decidedAt: ago(60 * 24 * 12 - 40), note: 'Đã mời Sato Yuki dạy thay.' });
const p2 = past('C-N3K12', 6);
if (p2) req.push({ id: 'CR-3', sessionId: p2.id, classId: p2.classId, teacherId: 't-ha', reasonType: 'Khác', reason: 'Tham dự hội thảo giáo viên tiếng Nhật.', status: 'rejected', createdAt: ago(60 * 24 * 20), decidedBy: 'Vũ Ngọc Mai', decidedAt: ago(60 * 24 * 19), note: 'Lớp sắp thi giữa kỳ, không tìm được GV thay — mong cô sắp xếp.' });
export const MOCK_CANCEL_REQUESTS = req;

// ---------------- Lời mời dạy thay gửi tới t-ha ----------------

const inv: SubInvite[] = [];
const other = MOCK_SESSIONS.filter((s) => s.date > today && s.status === 'scheduled' && ['C-N4K15', 'C-N5K21', 'C-N4K14', 'C-N5K22'].includes(s.classId));
const pick = (k: number) => other[k * 3];
[
  { k: 0, from: 't-long', reason: 'GV Phạm Đức Long đi công tác Đà Nẵng', status: 'pending' as const },
  { k: 2, from: 't-anh', reason: 'GV Trần Mai Anh nghỉ phép cưới', status: 'pending' as const },
].forEach((x, i) => {
  const s = pick(x.k);
  if (s) inv.push({ id: `SI-${i + 1}`, sessionId: s.id, classId: s.classId, fromTeacherId: x.from, toTeacherId: 't-ha', reason: x.reason, invitedBy: 'Vũ Ngọc Mai', createdAt: ago(60 * (3 + i * 5)), respondBy: dayjs(s.date).subtract(1, 'day').hour(12).toISOString(), status: x.status });
});
const pastOther = MOCK_SESSIONS.filter((s) => s.date < today && s.status === 'completed' && s.classId === 'C-N4K15').slice(-2);
if (pastOther[0]) inv.push({ id: 'SI-3', sessionId: pastOther[0].id, classId: pastOther[0].classId, fromTeacherId: 't-long', toTeacherId: 't-ha', reason: 'GV Phạm Đức Long ốm', invitedBy: 'Phạm Thị Hồng', createdAt: ago(60 * 24 * 9), respondBy: ago(60 * 24 * 8), status: 'declined', respondedAt: ago(60 * 24 * 9 - 30), declineReason: 'Trùng lịch dạy lớp N2-K05.' });
export const MOCK_SUB_INVITES = inv;

// ---------------- Tài liệu GV ----------------

export const MOCK_TEACHER_DOCS: TeacherDoc[] = [
  { id: 'TD-1', teacherId: 't-ha', title: 'Tổng hợp ngữ pháp ～ば・～たら・～なら', description: 'Bảng so sánh 3 mẫu điều kiện + 20 câu luyện tập có đáp án.', fileName: 'N3_jouken_bunpou.pdf', kind: 'pdf', size: 1_240_000, classIds: ['C-N3K12'], createdAt: ago(60 * 24 * 6), viewedBy: ['ST-1', 'ST-9', 'ST-13'] },
  { id: 'TD-2', teacherId: 't-ha', title: 'Audio luyện nghe Choukai — đề 3', fileName: 'choukai_de3.mp3', kind: 'audio', size: 8_900_000, classIds: ['C-N3K12'], createdAt: ago(60 * 24 * 2), viewedBy: ['ST-1'] },
  { id: 'TD-3', teacherId: 't-ha', title: 'Từ vựng chủ đề công việc (N2)', description: 'Flashcard dạng bảng, 120 từ.', fileName: 'N2_shigoto_goi.xlsx', kind: 'doc', size: 86_000, classIds: ['C-N2K05'], createdAt: ago(60 * 24 * 15), viewedBy: [] },
  { id: 'TD-4', teacherId: 't-ha', title: 'Video bài giảng: kính ngữ 尊敬語・謙譲語', fileName: 'https://youtu.be/keigo-n2', kind: 'link', url: 'https://youtu.be/keigo-n2', classIds: ['C-N2K05', 'C-N3K12'], createdAt: ago(60 * 24 * 25), viewedBy: ['ST-1'] },
];

// ---------------- Đơn xin nghỉ ----------------

const leave: LeaveRequest[] = [];
const sp = past('C-N3K12', 4);
if (sp) leave.push({ id: 'LR-1', studentId: 'ST-1', classId: 'C-N3K12', sessionId: sp.id, reasonType: 'Ốm / sức khỏe', reason: 'Con bị sốt, xin nghỉ 1 buổi.', by: 'parent', byName: 'Nguyễn Văn Bình', status: 'approved', createdAt: ago(60 * 24 * 10), decidedBy: 'Vũ Ngọc Mai', decidedAt: ago(60 * 24 * 10 - 50) });
const sf = future('C-N3K12', 4);
if (sf) leave.push({ id: 'LR-2', studentId: 'ST-1', classId: 'C-N3K12', sessionId: sf.id, reasonType: 'Trùng lịch học / làm', reason: 'Trùng lịch thi học kỳ ở trường.', by: 'student', byName: 'Nguyễn Văn An', status: 'pending', createdAt: ago(60 * 5) });
const kf = future('C-KIDS05', 1);
if (kf) leave.push({ id: 'LR-3', studentId: 'ST-2', classId: 'C-KIDS05', sessionId: kf.id, reasonType: 'Việc gia đình', reason: 'Gia đình về quê dự đám cưới.', by: 'parent', byName: 'Nguyễn Văn Bình', status: 'pending', createdAt: ago(60 * 26) });
/** Bản ghi điểm danh của buổi gốc khớp với đơn đã duyệt (dữ liệu giả phải nhất quán) */
const markLeave = (sessionId: string, studentId: string, lrId: string, reasonType: string, madeUp?: { sessionId: string; classCode: string; status: 'present' | 'late' }) => {
  const s = MOCK_SESSIONS.find((x) => x.id === sessionId);
  const r = s?.records?.find((x) => x.studentId === studentId);
  if (r) Object.assign(r, { status: 'excused', note: `Có đơn xin nghỉ — ${reasonType}`, leaveRequestId: lrId, madeUp, checkInAt: undefined, method: undefined });
  if (s?.records) s.attendance = { present: s.records.filter((x) => x.status === 'present' || x.status === 'late').length, total: s.records.length };
};
if (sp) markLeave(sp.id, 'ST-1', 'LR-1', 'Ốm / sức khỏe');

/**
 * Kịch bản HỌC BÙ: 1 HV lớp X (khóa có ≥ 2 lớp đang học, lịch không trùng nhau)
 *  - LR-4: đã nghỉ 1 buổi của X, đã học bù ở lớp Y (có mặt) → buổi gốc "có phép · đã học bù"
 *  - LR-5: sắp nghỉ 1 buổi của X, đã đăng ký học bù ở Y → GV lớp Y thấy HV trong danh sách điểm danh buổi đó
 */
const toMin = (t: string) => Number(t.slice(0, 2)) * 60 + Number(t.slice(3, 5));
const clash = (a: { date: string; startTime: string; endTime: string }, b: { date: string; startTime: string; endTime: string }) =>
  a.date === b.date && toMin(a.startTime) < toMin(b.endTime) && toMin(b.startTime) < toMin(a.endTime);
(() => {
  const ongoing = MOCK_CLASSES.filter((c) => c.status === 'ongoing');
  for (const x of ongoing) {
    const y = ongoing.find((c) => c.id !== x.id && c.courseId === x.courseId && !x.weekdays.some((d) => c.weekdays.includes(d)));
    const enr = y && MOCK_ENROLLMENTS.find((e) => e.classId === x.id && e.status === 'studying' && !['ST-1', 'ST-2'].includes(e.studentId));
    if (!y || !enr) continue;
    const xs = MOCK_SESSIONS.filter((s) => s.classId === x.id && s.kind === 'lesson' && s.status !== 'cancelled');
    const ys = MOCK_SESSIONS.filter((s) => s.classId === y.id && s.kind === 'lesson' && s.status !== 'cancelled');
    const free = (s: (typeof ys)[number]) => !xs.some((o) => clash(o, s));
    const pastX = xs.filter((s) => s.date < today && s.records?.some((r) => r.studentId === enr.studentId)).slice(-3)[0];
    const pastY = pastX && ys.filter((s) => s.date > pastX.date && s.date < today && s.records && free(s))[0];
    const futX = xs.filter((s) => s.date > today && s.status === 'scheduled')[2];
    const futY = futX && ys.filter((s) => s.date > today && s.status === 'scheduled' && free(s) && s.date >= dayjs(futX.date).subtract(3, 'day').format('YYYY-MM-DD'))[0];
    // Cần cặp lớp có cả buổi đã điểm danh (LR-4) lẫn buổi sắp tới (LR-5) — không thì thử lớp khác
    if (!pastX || !pastY || !futX || !futY) continue;
    const code = y.code.replace(/^GV[VN]*_/, '');
    {
      leave.push({ id: 'LR-4', studentId: enr.studentId, classId: x.id, sessionId: pastX.id, reasonType: 'Đi xa / công tác', reason: 'Đi công tác Hải Phòng 2 ngày.', by: 'student', byName: 'Học viên', status: 'approved', createdAt: dayjs(pastX.date).subtract(3, 'day').toISOString(), decidedBy: 'Vũ Ngọc Mai', decidedAt: dayjs(pastX.date).subtract(2, 'day').toISOString(), makeupSessionId: pastY.id, makeupResult: 'present' });
      markLeave(pastX.id, enr.studentId, 'LR-4', 'Đi xa / công tác', { sessionId: pastY.id, classCode: code, status: 'present' });
      pastY.records!.push({ studentId: enr.studentId, status: 'present', checkInAt: pastY.startTime, method: 'qr', makeupFor: { sessionId: pastX.id, classId: x.id, leaveRequestId: 'LR-4' } });
      pastY.attendance = { present: pastY.records!.filter((r) => r.status === 'present' || r.status === 'late').length, total: pastY.records!.length };
    }
    leave.push({ id: 'LR-5', studentId: enr.studentId, classId: x.id, sessionId: futX.id, reasonType: 'Trùng lịch học / làm', reason: 'Tăng ca cuối tháng.', by: 'student', byName: 'Học viên', status: 'approved', createdAt: ago(60 * 30), decidedBy: 'Vũ Ngọc Mai', decidedAt: ago(60 * 28), makeupSessionId: futY.id });
    break;
  }
})();

export const MOCK_LEAVE_REQUESTS = leave;

// ---------------- Flashcard ----------------

const deck = (id: string, title: string, level: FlashDeck['level'], kind: FlashDeck['kind'], lesson: string, rows: [string, string, string, string?][]): FlashDeck => ({
  id, title, level, kind, lesson,
  cards: rows.map(([front, reading, meaning, example], i) => ({ id: `${id}-${i + 1}`, front, reading, meaning, example })),
});

export const MOCK_DECKS: FlashDeck[] = [
  deck('D-N3-L10', 'Từ vựng bài 10 — Công việc & xã hội', 'N3', 'vocab', 'Bài 10', [
    ['締め切り', 'しめきり', 'hạn chót', 'レポートの締め切りは金曜日だ。'],
    ['残業', 'ざんぎょう', 'làm thêm giờ', '今日は残業しなければならない。'],
    ['給料', 'きゅうりょう', 'tiền lương'],
    ['面接', 'めんせつ', 'phỏng vấn', '明日、会社の面接がある。'],
    ['募集', 'ぼしゅう', 'tuyển dụng, chiêu mộ'],
    ['経験', 'けいけん', 'kinh nghiệm'],
    ['責任', 'せきにん', 'trách nhiệm'],
    ['取引先', 'とりひきさき', 'đối tác kinh doanh'],
    ['出張', 'しゅっちょう', 'đi công tác', '来週大阪へ出張する。'],
    ['会議', 'かいぎ', 'cuộc họp'],
  ]),
  deck('D-N3-L11', 'Từ vựng bài 11 — Sức khỏe', 'N3', 'vocab', 'Bài 11', [
    ['怪我', 'けが', 'vết thương, bị thương'],
    ['症状', 'しょうじょう', 'triệu chứng'],
    ['熱', 'ねつ', 'sốt', '熱が３８度ある。'],
    ['咳', 'せき', 'ho'],
    ['薬局', 'やっきょく', 'hiệu thuốc'],
    ['診察', 'しんさつ', 'khám bệnh'],
    ['健康', 'けんこう', 'sức khỏe'],
    ['入院', 'にゅういん', 'nhập viện'],
  ]),
  deck('D-N3-K12', 'Kanji bài 12', 'N3', 'kanji', 'Bài 12', [
    ['届', 'とど・く', 'gửi tới, đến nơi', '荷物が届いた。'],
    ['届ける', 'とどける', 'chuyển tới, giao'],
    ['過', 'す・ぎる / カ', 'quá, vượt qua'],
    ['経', 'へ・る / ケイ', 'trải qua'],
    ['得', 'え・る / トク', 'được, đạt được'],
    ['許', 'ゆる・す / キョ', 'cho phép'],
    ['違', 'ちが・う / イ', 'khác, sai'],
    ['退', 'しりぞ・く / タイ', 'rút lui'],
  ]),
  deck('D-N4-L20', 'Ôn N4 — Động từ thường gặp', 'N4', 'vocab', 'Ôn tập', [
    ['集める', 'あつめる', 'thu thập'],
    ['選ぶ', 'えらぶ', 'chọn'],
    ['届く', 'とどく', 'đến nơi'],
    ['間に合う', 'まにあう', 'kịp giờ'],
    ['謝る', 'あやまる', 'xin lỗi'],
    ['褒める', 'ほめる', 'khen'],
    ['比べる', 'くらべる', 'so sánh'],
    ['払う', 'はらう', 'trả tiền'],
  ]),
];

/** Tiến độ ôn của ST-1: đã học deck bài 10, một phần bài 11 */
const due = (d: number) => dayjs().add(d, 'day').startOf('day').toISOString();
export const MOCK_CARD_PROGRESS: Record<string, CardProgress> = {
  ...Object.fromEntries(MOCK_DECKS[0].cards.map((c, i) => [c.id, { box: [3, 2, 4, 1, 2, 3, 0, 1, 2, 5][i], due: due([2, 0, 6, -1, 0, 3, 0, -1, 1, 14][i]), reviews: 4 + i, lapses: i % 3 }])),
  ...Object.fromEntries(MOCK_DECKS[1].cards.slice(0, 4).map((c, i) => [c.id, { box: 1, due: due(i - 1), reviews: 1, lapses: 0 }])),
};
/** Ngày đã ôn (streak) */
export const MOCK_STUDY_DAYS: string[] = [0, 1, 2, 3, 4, 6, 7, 8, 10, 11, 12, 13, 14, 15].map((d) => dayjs().subtract(d, 'day').format('YYYY-MM-DD')).filter((d, i) => i > 0 || d !== today);
