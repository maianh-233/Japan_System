import type { ThemeConfig } from 'antd';

/** Bảng màu — đồng bộ với docs/design-system.md và src/assets/styles/index.css */
export const colors = {
  primary: {
    50: '#F3F8FE',
    100: '#EAF4FF',
    200: '#CFE4FB',
    300: '#A3CBF5',
    400: '#5FA2E8',
    500: '#2B7FD9',
    600: '#1F6FC5',
    700: '#1A5FAD',
    800: '#174C88',
    900: '#143D6B',
  },
  page: '#F5F9FF',
  surface: '#FFFFFF',
  line: '#E2E8F0',
  lineStrong: '#CBD5E1',
  ink: '#1E293B',
  inkSecondary: '#475569',
  inkMuted: '#64748B',
  inkPlaceholder: '#94A3B8',
  disabled: '#F1F5F9',
  success: '#15803D',
  warning: '#B45309',
  danger: '#DC2626',
} as const;

export const fontFamily =
  '"Be Vietnam Pro", "Noto Sans JP", system-ui, -apple-system, "Segoe UI", sans-serif';

export const antdTheme: ThemeConfig = {
  cssVar: true,
  hashed: false,
  token: {
    colorPrimary: colors.primary[600],
    colorInfo: colors.primary[600],
    colorSuccess: colors.success,
    colorWarning: colors.warning,
    colorError: colors.danger,
    colorLink: colors.primary[600],
    colorText: colors.ink,
    colorTextSecondary: colors.inkSecondary,
    colorTextTertiary: colors.inkMuted,
    colorTextPlaceholder: colors.inkPlaceholder,
    colorBorder: colors.line,
    colorBorderSecondary: colors.line,
    colorBgLayout: colors.page,
    colorBgContainer: colors.surface,
    colorBgContainerDisabled: colors.disabled,
    fontFamily,
    fontSize: 14,
    borderRadius: 8,
    borderRadiusLG: 12,
    borderRadiusSM: 6,
    controlHeight: 40,
    controlHeightLG: 44,
    controlHeightSM: 32,
    controlOutline: 'rgba(43, 127, 217, 0.25)',
    controlOutlineWidth: 3,
    boxShadow: '0 4px 12px rgba(15, 40, 80, 0.08)',
    boxShadowSecondary: '0 12px 32px rgba(15, 40, 80, 0.12)',
    motionDurationMid: '0.18s',
  },
  components: {
    Input: { activeBorderColor: colors.primary[600], hoverBorderColor: colors.primary[400] },
    InputNumber: { activeBorderColor: colors.primary[600], hoverBorderColor: colors.primary[400] },
    Pagination: { itemActiveBg: colors.primary[100] },
    Tooltip: { colorBgSpotlight: colors.ink },
  },
};
