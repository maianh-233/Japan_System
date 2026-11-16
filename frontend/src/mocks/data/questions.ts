import dayjs from 'dayjs';
import type { QDifficulty, QFormat, QLevel, QSkill, QStatus, Question } from '@/features/academic/questions/types';

/**
 * Dữ liệu giả — Ngân hàng câu hỏi kiểu đề JLPT (N5 → N1).
 * Furigana: 漢字{かんじ}. Từ được hỏi: **...**. Có đủ: đọc Kanji, chọn từ, ngữ pháp, sắp xếp ★, đọc hiểu (đoạn văn), nghe hiểu (audio + lời thoại).
 * Trạng thái: phần lớn đã duyệt; vài câu chờ duyệt / nháp / ngừng dùng (lộ đề).
 */

const ago = (d: number) => dayjs().subtract(d, 'day').hour(10).minute(0).second(0).toISOString();
const CUR: Record<QLevel, string> = { N5: 'CUR-N5-V2', N4: 'CUR-N4-V2', N3: 'CUR-N3-V1', N2: 'CUR-N2', N1: 'CUR-N1' };
const AUTHOR: Record<QLevel, string> = { N5: 'Trần Mai Anh', N4: 'Phạm Đức Long', N3: 'Nguyễn Thu Hà', N2: 'Nguyễn Thu Hà', N1: 'Sato Yuki' };
const RATE: Record<QDifficulty, number> = { easy: 0.86, medium: 0.63, hard: 0.34 };

interface Seed {
  level: QLevel;
  skill: QSkill;
  format?: QFormat;
  difficulty: QDifficulty;
  stem: string;
  options: string[];
  answer: number;
  explanation?: string;
  passage?: string;
  audio?: { name: string; durationSec: number };
  transcript?: string;
  lesson?: number;
  tags?: string[];
  status?: QStatus;
  review?: { by: string; note?: string };
}

const seeds: Seed[] = [
  // ======================= N5 =======================
  { level: 'N5', skill: 'kanji', difficulty: 'easy', stem: '**学校**へ　いきます。', options: ['がっこう', 'がこう', 'がっこ', 'かっこう'], answer: 0, explanation: '学校（がっこう）: trường học. Chú ý âm ngắt っ.', lesson: 3, tags: ['Đọc Kanji'] },
  { level: 'N5', skill: 'kanji', difficulty: 'easy', stem: 'にちようびに　**山**に　のぼりました。', options: ['かわ', 'やま', 'うみ', 'そら'], answer: 1, explanation: '山（やま）: núi. 川 = かわ, 海 = うみ, 空 = そら.', lesson: 5, tags: ['Đọc Kanji'] },
  { level: 'N5', skill: 'kanji', difficulty: 'medium', stem: 'まいにち　**しんぶん**を　よみます。', options: ['新文', '新聞', '親聞', '新問'], answer: 1, explanation: 'しんぶん = 新聞 (tờ báo). 聞 gồm bộ 門 + 耳.', lesson: 6, tags: ['Viết Kanji', 'Dễ nhầm'] },
  { level: 'N5', skill: 'vocab', difficulty: 'easy', stem: 'へやが　くらいですから、でんきを（　　）ください。', options: ['つけて', 'けして', 'しめて', 'あけて'], answer: 0, explanation: 'Phòng tối → bật đèn: でんきを つける. けす = tắt.', lesson: 7, tags: ['Động từ'] },
  { level: 'N5', skill: 'vocab', difficulty: 'easy', stem: 'この　りんごは　（　　）です。ひとつ　50えんです。', options: ['たかい', 'やすい', 'おもい', 'ひろい'], answer: 1, explanation: '50 yên một quả → rẻ (やすい).', lesson: 8, tags: ['Tính từ'] },
  { level: 'N5', skill: 'grammar', difficulty: 'easy', stem: 'わたしは　毎朝{まいあさ}　コーヒー（　　）飲{の}みます。', options: ['を', 'に', 'が', 'で'], answer: 0, explanation: 'Tân ngữ trực tiếp của 飲みます dùng trợ từ を.', lesson: 6, tags: ['Trợ từ'] },
  { level: 'N5', skill: 'grammar', difficulty: 'easy', stem: 'きのうは　雨{あめ}（　　）。', options: ['です', 'でした', 'だ', 'ではありません'], answer: 1, explanation: 'きのう (hôm qua) → thể quá khứ でした.', lesson: 4, tags: ['Thì quá khứ'], status: 'retired', review: { by: 'Nguyễn Thu Hà', note: 'Lộ đề — câu xuất hiện trong nhóm Zalo học viên, thay bằng câu tương đương.' } },
  { level: 'N5', skill: 'grammar', difficulty: 'medium', stem: '図書館{としょかん}（　　）本{ほん}を　読{よ}みます。', options: ['で', 'に', 'へ', 'を'], answer: 0, explanation: 'Nơi diễn ra hành động dùng で.', lesson: 6, tags: ['Trợ từ'] },
  { level: 'N5', skill: 'grammar', format: 'star', difficulty: 'medium', stem: 'この　＿＿　＿＿　★　＿＿　ですか。', options: ['は', 'かばん', 'の', 'だれ'], answer: 3, explanation: 'この かばん は だれ の ですか → vị trí ★ là だれ.', lesson: 2, tags: ['Sắp xếp câu'] },
  {
    level: 'N5',
    skill: 'reading',
    difficulty: 'easy',
    passage: 'わたしは　毎朝{まいあさ}　7時{じ}に　起{お}きます。朝{あさ}ごはんを　食{た}べてから、8時{じ}に　うちを　出{で}ます。会社{かいしゃ}まで　電車{でんしゃ}で　30分{ぷん}です。',
    stem: 'この人は　何時{なんじ}に　うちを　出{で}ますか。',
    options: ['7時', '7時半', '8時', '8時半'],
    answer: 2,
    explanation: '「8時に うちを 出ます」. 8時半 là giờ đến công ty (8時 + 30分).',
    lesson: 4,
    tags: ['Đọc ngắn'],
  },
  {
    level: 'N5',
    skill: 'listening',
    difficulty: 'medium',
    audio: { name: 'N5-nghe-01.mp3', durationSec: 42 },
    transcript: '女：すみません、このノートはいくらですか。\n男：ひとつ120円です。三つで300円です。\n女：じゃあ、三つください。',
    stem: '女{おんな}の人{ひと}は　いくら　はらいますか。',
    options: ['120円', '240円', '300円', '360円'],
    answer: 2,
    explanation: 'Mua 3 quyển với giá combo 300 yên (không phải 120 × 3 = 360).',
    lesson: 11,
    tags: ['Nghe số'],
  },
  { level: 'N5', skill: 'vocab', difficulty: 'medium', stem: 'あした　ともだちに　てがみを　（　　）。', options: ['かきます', 'ききます', 'よみます', 'みます'], answer: 0, lesson: 7, tags: ['Động từ'], status: 'draft' },

  // ======================= N4 =======================
  { level: 'N4', skill: 'kanji', difficulty: 'easy', stem: '週末{しゅうまつ}は　**家族**と　旅行{りょこう}します。', options: ['かぞく', 'かそく', 'がぞく', 'かぞう'], answer: 0, lesson: 1, tags: ['Đọc Kanji'] },
  { level: 'N4', skill: 'kanji', difficulty: 'medium', stem: 'この　店{みせ}には　**特別**な　料理{りょうり}が　あります。', options: ['とくへつ', 'どくべつ', 'とくべつ', 'とっべつ'], answer: 2, explanation: '特別（とくべつ）: đặc biệt — chú ý べ có dakuten.', lesson: 3, tags: ['Đọc Kanji', 'Âm đục'] },
  { level: 'N4', skill: 'vocab', difficulty: 'easy', stem: 'かぜを　ひいて、ねつが（　　）。', options: ['でました', 'なりました', 'きました', 'はいりました'], answer: 0, explanation: 'Cụm cố định: ねつが でる (bị sốt).', lesson: 2, tags: ['Cụm cố định'] },
  { level: 'N4', skill: 'vocab', difficulty: 'medium', stem: 'あしたの　会議{かいぎ}に　（　　）するか　どうか、メールで　知{し}らせて　ください。', options: ['しゅっせき', 'しゅっぱつ', 'しゅうり', 'しょうたい'], answer: 0, explanation: '出席（しゅっせき）: tham dự.', lesson: 6, tags: ['Danh từ する'], status: 'review' },
  { level: 'N4', skill: 'grammar', difficulty: 'easy', stem: '雨{あめ}が　降{ふ}って　いる（　　）、出{で}かけません。', options: ['ので', 'のに', 'けど', 'でも'], answer: 0, explanation: 'ので: chỉ nguyên nhân khách quan.', lesson: 4, tags: ['Liên từ'] },
  { level: 'N4', skill: 'grammar', difficulty: 'medium', stem: 'この　本{ほん}は　先生{せんせい}に　（　　）。', options: ['いただきました', 'くださいました', 'さしあげました', 'やりました'], answer: 0, explanation: 'Nhận từ người trên: 先生に いただきました. くださいました đi với 先生が.', lesson: 7, tags: ['Kính ngữ', 'Cho nhận'] },
  { level: 'N4', skill: 'grammar', difficulty: 'easy', stem: '日本{にほん}へ　行{い}った（　　）が　あります。', options: ['こと', 'もの', 'ところ', 'はず'], answer: 0, explanation: 'Vた ことが あります: đã từng làm gì.', lesson: 3, tags: ['Kinh nghiệm'] },
  { level: 'N4', skill: 'grammar', format: 'star', difficulty: 'hard', stem: '駅{えき}に　＿＿　＿＿　★　＿＿　電話{でんわ}して　ください。', options: ['ら', '着{つ}いた', 'すぐ', 'わたしに'], answer: 2, explanation: '駅に 着いた ら すぐ わたしに 電話してください → ★ là すぐ.', lesson: 8, tags: ['Sắp xếp câu', 'Điều kiện たら'] },
  {
    level: 'N4',
    skill: 'reading',
    difficulty: 'medium',
    passage: '来週{らいしゅう}の　日曜日{にちようび}に　公園{こうえん}で　さくら祭{まつ}りが　あります。雨{あめ}の　場合{ばあい}は　次{つぎ}の　週{しゅう}に　なります。参加{さんか}したい　人{ひと}は　金曜日{きんようび}までに　事務所{じむしょ}に　申{もう}し込{こ}んで　ください。',
    stem: '祭{まつ}りに　参加{さんか}したい　人{ひと}は　どう　しなければ　なりませんか。',
    options: ['日曜日に公園へ行く', '金曜日までに事務所に申し込む', '雨の日に事務所に電話する', '次の週に申し込む'],
    answer: 1,
    lesson: 9,
    tags: ['Thông báo'],
  },
  {
    level: 'N4',
    skill: 'listening',
    difficulty: 'medium',
    audio: { name: 'N4-nghe-03.mp3', durationSec: 58 },
    transcript: '男：あしたの会議、10時からでしたよね。\n女：いえ、部長が午前中出張なので、午後2時からに変わりました。\n男：わかりました。資料は何部必要ですか。\n女：8部お願いします。',
    stem: '会議{かいぎ}は　何時{なんじ}からですか。',
    options: ['10時', '12時', '2時', '8時'],
    answer: 2,
    explanation: 'Đổi sang 午後2時. "8部" là số bản tài liệu — bẫy.',
    lesson: 10,
    tags: ['Nghe bẫy số'],
  },

  // ======================= N3 =======================
  { level: 'N3', skill: 'kanji', difficulty: 'easy', stem: '彼{かれ}の　意見{いけん}に　**賛成**です。', options: ['さんせい', 'さんぜい', 'さんせ', 'ざんせい'], answer: 0, lesson: 2, tags: ['Đọc Kanji'] },
  { level: 'N3', skill: 'kanji', difficulty: 'medium', stem: '**努力**した　結果{けっか}、試験{しけん}に　合格{ごうかく}できた。', options: ['どりょく', 'どうりょく', 'どりき', 'とりょく'], answer: 0, explanation: '努力（どりょく）: nỗ lực — không có trường âm.', lesson: 3, tags: ['Đọc Kanji', 'Trường âm'] },
  { level: 'N3', skill: 'vocab', difficulty: 'medium', stem: '約束{やくそく}の　時間{じかん}に　（　　）しまい、友達{ともだち}を　待{ま}たせた。', options: ['遅れて', '遅くて', '延ばして', '過ぎて'], answer: 0, explanation: '時間に遅れる: trễ giờ.', lesson: 4, tags: ['Động từ'] },
  { level: 'N3', skill: 'vocab', difficulty: 'medium', stem: 'この　仕事{しごと}には　ある程度{ていど}の　（　　）が　必要{ひつよう}だ。', options: ['経験', '経済', '経由', '経営'], answer: 0, explanation: '経験（けいけん）: kinh nghiệm.', lesson: 5, tags: ['Danh từ Hán'], status: 'review' },
  { level: 'N3', skill: 'grammar', difficulty: 'hard', stem: '忙{いそが}しい（　　）、毎日{まいにち}　日本語{にほんご}を　勉強{べんきょう}して　いる。', options: ['にもかかわらず', 'によって', 'について', 'にとって'], answer: 0, explanation: 'にもかかわらず: mặc dù... (trang trọng).', lesson: 6, tags: ['Mẫu câu N3'] },
  { level: 'N3', skill: 'grammar', difficulty: 'medium', stem: '先生{せんせい}の　説明{せつめい}の　（　　）、やっと　わかった。', options: ['おかげで', 'せいで', 'ために', 'ように'], answer: 0, explanation: 'Kết quả tích cực nhờ ai → おかげで. せいで dùng cho kết quả xấu.', lesson: 6, tags: ['Mẫu câu N3', 'Dễ nhầm'] },
  { level: 'N3', skill: 'grammar', format: 'star', difficulty: 'hard', stem: 'この　問題{もんだい}は　＿＿　＿＿　★　＿＿　難{むずか}しい。', options: ['とって', '子{こ}ども', 'かなり', 'に'], answer: 0, explanation: '子ども に とって かなり 難しい → ★ là とって.', lesson: 7, tags: ['Sắp xếp câu'] },
  {
    level: 'N3',
    skill: 'reading',
    difficulty: 'medium',
    passage: '最近{さいきん}、自宅{じたく}で　働{はたら}く　人{ひと}が　増{ふ}えている。通勤{つうきん}の　時間{じかん}が　なくなり、家族{かぞく}と　過{す}ごす　時間{じかん}が　増{ふ}えたという　声{こえ}が　多{おお}い。一方{いっぽう}で、同僚{どうりょう}と　話{はな}す　機会{きかい}が　減{へ}り、孤独{こどく}を　感{かん}じる　人{ひと}も　いる。',
    stem: '自宅{じたく}で　働{はたら}く　人{ひと}について、筆者{ひっしゃ}は　何{なに}と　言{い}っていますか。',
    options: ['通勤が楽になったので仕事が増えた', 'よい点もあるが、困る点もある', '家族と過ごす時間が減った', '同僚と話す機会が増えた'],
    answer: 1,
    explanation: '一方で → đoạn văn nêu cả mặt tốt và mặt chưa tốt.',
    lesson: 8,
    tags: ['Đọc ý chính'],
  },
  {
    level: 'N3',
    skill: 'listening',
    difficulty: 'hard',
    audio: { name: 'N3-nghe-05.mp3', durationSec: 71 },
    transcript: '女：課長、明日の出張ですが、新幹線の切符はもう買いましたか。\n男：いや、まだなんだ。悪いけど、ホテルの予約を先にしてくれる？切符は僕が帰りに買うから。\n女：わかりました。',
    stem: '女{おんな}の人{ひと}は　まず　何{なに}を　しますか。',
    options: ['新幹線の切符を買う', 'ホテルを予約する', '課長に電話する', '出張の資料を作る'],
    answer: 1,
    lesson: 9,
    tags: ['Nghe việc cần làm'],
  },

  // ======================= N2 =======================
  { level: 'N2', skill: 'kanji', difficulty: 'medium', stem: '経済{けいざい}の　**動向**を　注視{ちゅうし}する。', options: ['どうこう', 'どうむき', 'とうこう', 'どうごう'], answer: 0, lesson: 2, tags: ['Đọc Kanji', 'Kinh tế'] },
  { level: 'N2', skill: 'grammar', difficulty: 'hard', stem: 'この　プロジェクトは　彼{かれ}の　協力{きょうりょく}（　　）成功{せいこう}しなかった　だろう。', options: ['なしには', 'ぬきで', 'をもとに', 'に際して'], answer: 0, explanation: 'NなしにはVない: nếu không có N thì không thể V.', lesson: 3, tags: ['Mẫu câu N2'] },
  { level: 'N2', skill: 'vocab', difficulty: 'hard', stem: '新{あたら}しい　制度{せいど}は　（　　）に　導入{どうにゅう}された。', options: ['円滑', '円満', '温和', '穏便'], answer: 0, explanation: '円滑（えんかつ）に: một cách suôn sẻ (công việc, thủ tục).', lesson: 4, tags: ['Từ Hán dễ nhầm'], status: 'review' },

  // ======================= N1 =======================
  { level: 'N1', skill: 'grammar', difficulty: 'hard', stem: '社長{しゃちょう}の　発言{はつげん}は、誤解{ごかい}を　招{まね}き（　　）ものだった。', options: ['かねない', 'かねる', 'がたい', 'っこない'], answer: 0, explanation: 'Vます + かねない: có khả năng gây ra (điều xấu).', lesson: 1, tags: ['Mẫu câu N1'] },
  { level: 'N1', skill: 'vocab', difficulty: 'hard', stem: '彼{かれ}の　態度{たいど}は　（　　）として　変{か}わらなかった。', options: ['依然', '断然', '漠然', '歴然'], answer: 0, explanation: '依然として: vẫn như cũ.', lesson: 2, tags: ['Phó từ'], status: 'draft' },
];

export const MOCK_QUESTIONS: Question[] = seeds.map((q, i) => {
  const status = q.status ?? 'approved';
  const attempts = status === 'approved' || status === 'retired' ? 30 + ((i * 37) % 260) : 0;
  const rate = Math.min(0.97, Math.max(0.12, RATE[q.difficulty] + (((i * 13) % 17) - 8) / 100));
  const correct = Math.round(attempts * rate);
  // Phân bố chọn đáp án: đáp án đúng + phần còn lại chia cho đáp án nhiễu
  const wrong = attempts - correct;
  const picks = q.options.map((_, k) => (k === q.answer ? correct : 0));
  const others = q.options.map((_, k) => k).filter((k) => k !== q.answer);
  others.forEach((k, j) => (picks[k] = j === others.length - 1 ? wrong - picks.reduce((s, x, idx) => s + (idx !== q.answer ? x : 0), 0) : Math.round(wrong * [0.55, 0.3][j])));
  const level = q.level;
  const created = 200 - i * 5;
  return {
    id: `Q-${i + 1}`,
    code: `${level}-${q.skill.slice(0, 2).toUpperCase()}-${String(i + 1).padStart(4, '0')}`,
    level,
    skill: q.skill,
    format: q.format ?? 'choice',
    difficulty: q.difficulty,
    stem: q.stem,
    passage: q.passage,
    audio: q.audio,
    transcript: q.transcript,
    options: q.options,
    answer: q.answer,
    explanation: q.explanation,
    curriculumId: q.lesson ? CUR[level] : undefined,
    lessonIds: q.lesson && i % 9 !== 4 ? [`${CUR[level]}-L${q.lesson}`] : [],
    tags: q.tags ?? [],
    status,
    author: AUTHOR[level],
    createdAt: ago(created),
    updatedAt: ago(Math.max(1, created - 20)),
    review: q.review ? { ...q.review, at: ago(Math.max(1, created - 25)) } : status === 'approved' ? { by: 'Nguyễn Thu Hà (Tổ trưởng chuyên môn)', at: ago(Math.max(1, created - 22)) } : undefined,
    stats: { used: attempts ? 1 + ((i * 7) % 9) : 0, attempts, correct, picks: attempts ? picks : undefined },
  };
});
