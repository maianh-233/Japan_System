/**
 * Báo lỗi / thông tin cho thao tác API chạy NỀN từ store (không nằm trong form có sẵn chỗ báo lỗi).
 * `ApiReporter` (gắn trong AppProvider) đăng ký `message` của AntD App để dùng đúng theme.
 */
type Show = (text: string) => void;

let showError: Show = (text) => console.error(text);
let showInfo: Show = () => undefined;

export const registerReporter = (error: Show, info: Show) => {
  showError = error;
  showInfo = info;
};

export const reportError = (e: unknown) => showError(e instanceof Error ? e.message : 'Thao tác không thành công, vui lòng thử lại.');

export const reportInfo = (text: string) => showInfo(text);

/**
 * Cập nhật lạc quan: đã sửa store trước, chạy lệnh API; lỗi → hoàn tác (`undo`) + báo lỗi.
 * Trả kết quả API (hoặc undefined khi lỗi) để nơi gọi ghép bản server.
 */
export async function optimistic<T>(run: () => Promise<T>, undo: () => void): Promise<T | undefined> {
  try {
    return await run();
  } catch (e) {
    undo();
    reportError(e);
    return undefined;
  }
}
