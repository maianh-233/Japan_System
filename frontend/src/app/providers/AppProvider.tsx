import type { ReactNode } from 'react';
import { App as AntdApp, ConfigProvider } from 'antd';
import { StyleProvider } from '@ant-design/cssinjs';
import viVN from 'antd/locale/vi_VN';
import dayjs from 'dayjs';
import 'dayjs/locale/vi';
import { antdTheme } from '@/config/theme';

dayjs.locale('vi');

/** Bọc toàn ứng dụng: theme AntD, locale tiếng Việt, đưa style AntD vào @layer antd. */
export function AppProvider({ children }: { children: ReactNode }) {
  return (
    <StyleProvider layer>
      <ConfigProvider theme={antdTheme} locale={viVN}>
        <AntdApp>{children}</AntdApp>
      </ConfigProvider>
    </StyleProvider>
  );
}
