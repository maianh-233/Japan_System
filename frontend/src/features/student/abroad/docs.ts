/** Giấy tờ cần nộp theo chương trình (tách riêng để mock data dùng được, tránh import vòng) */
export const DOC_TEMPLATES: Record<'study' | 'work', string[]> = {
  study: ['Hộ chiếu', 'Bằng tốt nghiệp + học bạ', 'Chứng chỉ JLPT / NAT', 'Giấy khai sinh', 'Sổ tiết kiệm (chứng minh tài chính)', 'Xác nhận thu nhập người bảo lãnh', 'Ảnh 4×3'],
  work: ['Hộ chiếu', 'CCCD', 'Sơ yếu lý lịch', 'Giấy khám sức khỏe', 'Bằng cấp', 'Chứng chỉ JLPT', 'Chứng chỉ kỹ năng ngành', 'Ảnh 4×3'],
};
export const docTemplate = (p: 'study' | 'tokutei' | 'tts' | 'engineer') => DOC_TEMPLATES[p === 'study' ? 'study' : 'work'];
