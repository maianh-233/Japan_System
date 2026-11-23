import { useEffect, type ReactNode } from 'react';
import { App as AntdApp, ConfigProvider } from 'antd';
import { StyleProvider } from '@ant-design/cssinjs';
import viVN from 'antd/locale/vi_VN';
import dayjs from 'dayjs';
import 'dayjs/locale/vi';
import { antdTheme } from '@/config/theme';
import { registerReporter } from '@/lib/api/report';

dayjs.locale('vi');

/** Đăng ký nơi hiện lỗi cho thao tác API chạy nền từ store (lib/api/report.ts) */
function ApiReporter() {
  const { message } = AntdApp.useApp();
  useEffect(() => registerReporter((t) => void message.error(t), (t) => void message.info(t)), [message]);
  return null;
}

/** Bọc toàn ứng dụng: theme AntD, locale tiếng Việt, đưa style AntD vào @layer antd. */
export function AppProvider({ children }: { children: ReactNode }) {
  return (
    <StyleProvider layer>
      <ConfigProvider theme={antdTheme} locale={viVN}>
        <AntdApp>
          <ApiReporter />
          {children}
        </AntdApp>
      </ConfigProvider>
    </StyleProvider>
  );
}
