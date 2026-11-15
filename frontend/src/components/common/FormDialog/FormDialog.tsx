import { useState, type ReactNode } from 'react';
import { App, Drawer, Form, Modal, type FormInstance } from 'antd';
import { cn } from '@/lib/cn';
import { useBreakpoint } from '@/hooks/useBreakpoint';
import { Button } from '../Button/Button';
import type { ButtonAction } from '../Button/buttonActions';
import { useConfirm } from '../ConfirmDialog/ConfirmDialog';

export interface FormDialogProps<V extends object> {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  description?: ReactNode;
  /** Giá trị ban đầu — truyền bản ghi khi SỬA, bỏ trống khi THÊM */
  initialValues?: Partial<V>;
  /** Lưu dữ liệu. Throw lỗi → giữ form, báo lỗi. Thành công → đóng + toast. */
  onSubmit: (values: V) => Promise<void> | void;
  /** Nội dung form: các <Form.Item> */
  children: ReactNode;
  /** 1 hoặc 2 cột (từ tablet trở lên) */
  columns?: 1 | 2;
  submitAction?: ButtonAction;
  submitText?: string;
  successMessage?: string;
  /** Hỏi lại khi đóng mà form đã bị sửa */
  confirmOnDiscard?: boolean;
  /** Truyền form từ ngoài nếu cần đọc/ghi giá trị (Form.useForm) */
  form?: FormInstance<V>;
  /** Nút phụ ở footer bên trái (vd: "Lưu & thêm tiếp") */
  footerExtra?: ReactNode;
  width?: number;
}

type Kind = 'modal' | 'drawer';

function useFormDialog<V extends object>(p: FormDialogProps<V>) {
  const { message } = App.useApp();
  const [innerForm] = Form.useForm<V>();
  const form = p.form ?? innerForm;
  const [submitting, setSubmitting] = useState(false);
  const { confirm, confirmDialog } = useConfirm();

  const requestClose = async () => {
    if (submitting) return;
    if ((p.confirmOnDiscard ?? true) && form.isFieldsTouched()) {
      const ok = await confirm({
        title: 'Bỏ các thay đổi chưa lưu?',
        description: 'Dữ liệu bạn vừa nhập sẽ không được lưu lại.',
        tone: 'warning',
        confirmAction: 'cancel',
        confirmText: 'Bỏ thay đổi',
        cancelText: 'Tiếp tục sửa',
      });
      if (!ok) return;
    }
    p.onClose();
  };

  const handleFinish = async (values: V) => {
    setSubmitting(true);
    try {
      await p.onSubmit(values);
      message.success(p.successMessage ?? 'Đã lưu thành công');
      p.onClose();
    } catch (e) {
      message.error(e instanceof Error ? e.message : 'Lưu không thành công, vui lòng thử lại.');
    } finally {
      setSubmitting(false);
    }
  };

  const body = (
    <Form<V>
      form={form}
      layout="vertical"
      requiredMark
      preserve={false}
      initialValues={p.initialValues}
      onFinish={handleFinish}
      scrollToFirstError
      className={cn('grid grid-cols-1 gap-x-4', p.columns === 2 && 'md:grid-cols-2')}
    >
      {p.children}
      {/* cho phép Enter để submit */}
      <button type="submit" hidden aria-hidden tabIndex={-1} />
    </Form>
  );

  const footer = (
    <div className="flex flex-col-reverse gap-2 sm:flex-row sm:items-center">
      {p.footerExtra && <div className="flex flex-wrap gap-2 sm:mr-auto">{p.footerExtra}</div>}
      <div className="flex flex-col-reverse gap-2 sm:ml-auto sm:flex-row">
        <Button action="cancel" onClick={requestClose} disabled={submitting} block className="sm:w-auto" />
        <Button
          action={p.submitAction ?? 'save'}
          loading={submitting}
          onClick={() => form.submit()}
          block
          className="sm:w-auto"
        >
          {p.submitText}
        </Button>
      </div>
    </div>
  );

  const header = (
    <div className="flex flex-col gap-0.5 pr-8">
      <span className="text-base leading-6 font-semibold">{p.title}</span>
      {p.description && <span className="text-[13px] font-normal text-ink-muted">{p.description}</span>}
    </div>
  );

  return { body, footer, header, requestClose, confirmDialog, submitting };
}

function FormDialogBase<V extends object>({ kind, ...p }: FormDialogProps<V> & { kind: Kind }) {
  const { isMobile } = useBreakpoint();
  const { body, footer, header, requestClose, confirmDialog } = useFormDialog(p);
  const wide = p.columns === 2;

  // Modal trên desktop; mobile → drawer trượt từ dưới lên (dễ thao tác bằng ngón cái)
  if (kind === 'modal' && !isMobile) {
    return (
      <>
        <Modal
          open={p.open}
          onCancel={requestClose}
          maskClosable={false}
          title={header}
          footer={footer}
          width={p.width ?? (wide ? 760 : 520)}
          destroyOnHidden
          centered
          styles={{ body: { maxHeight: 'calc(100vh - 220px)', overflowY: 'auto', paddingTop: 8 } }}
        >
          {body}
        </Modal>
        {confirmDialog}
      </>
    );
  }

  return (
    <>
      <Drawer
        open={p.open}
        onClose={requestClose}
        maskClosable={false}
        title={header}
        footer={footer}
        placement={isMobile ? 'bottom' : 'right'}
        height={isMobile ? '92%' : undefined}
        width={isMobile ? undefined : (p.width ?? (wide ? 720 : 520))}
        destroyOnHidden
        rootClassName={isMobile ? '[&_.ant-drawer-content]:rounded-t-2xl' : undefined}
        styles={{ footer: { padding: '12px 16px' } }}
      >
        {body}
      </Drawer>
      {confirmDialog}
    </>
  );
}

/** Form trong hộp thoại giữa màn hình (desktop) — tự thành drawer đáy trên mobile. Dùng cho form ngắn. */
export function ModalForm<V extends object>(props: FormDialogProps<V>) {
  return <FormDialogBase<V> kind="modal" {...props} />;
}

/** Form trượt từ phải (desktop) / từ dưới (mobile). Dùng cho form dài, nhiều nhóm thông tin. */
export function DrawerForm<V extends object>(props: FormDialogProps<V>) {
  return <FormDialogBase<V> kind="drawer" {...props} />;
}
