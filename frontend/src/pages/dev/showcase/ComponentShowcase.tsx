import { useMemo, useState, type ReactNode } from 'react';
import { Form, Select, App } from 'antd';
import { Mail, Phone, Star, User } from 'lucide-react';
import {
  BUTTON_ACTIONS,
  Button,
  ImagePicker,
  NumberInput,
  Pagination,
  SearchBar,
  TextArea,
  TextInput,
  type ButtonAction,
  type ButtonVariant,
} from '@/components/common';
import { formatCurrency } from '@/lib/format';
import { StudentPageDemo } from './sections/StudentPageDemo';
import { FeedbackSection, NotifySection, PickersSection, TagsSection, TimetableSection } from './sections/MoreSections';

/** Trang xem thử component dùng chung — chỉ dùng khi phát triển (/dev/components). */
export default function ComponentShowcase() {
  return (
    <div className="mx-auto flex max-w-6xl flex-col gap-6 px-4 py-6 sm:px-6 sm:py-8">
      <header className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold leading-8 text-ink">Component dùng chung</h1>
        <p className="text-ink-muted">Xem thử theo design system — thu nhỏ cửa sổ để kiểm tra responsive.</p>
      </header>
      <nav
        aria-label="Mục lục"
        className="sticky top-0 z-20 -mx-4 flex gap-1 overflow-x-auto border-b border-line bg-page/95 px-4 py-2 backdrop-blur sm:mx-0 sm:rounded-lg sm:border"
      >
        {[
          ['page-demo', 'Trang mẫu'],
          ['buttons', 'Button'],
          ['inputs', 'Input'],
          ['pickers', 'Select · Ngày · File'],
          ['notify', 'Thông báo'],
          ['tags', 'Tag · Avatar'],
          ['feedback', 'Rỗng · Lỗi · Tải'],
          ['timetable', 'Thời khóa biểu'],
          ['images', 'Ảnh'],
          ['list', 'Tìm kiếm · Phân trang'],
        ].map(([id, label]) => (
          <a
            key={id}
            href={`#${id}`}
            className="shrink-0 rounded-md px-3 py-1.5 text-[13px] font-medium whitespace-nowrap text-ink-secondary hover:bg-primary-100 hover:text-primary-700"
          >
            {label}
          </a>
        ))}
      </nav>
      <section id="page-demo" className="scroll-mt-20">
        <StudentPageDemo />
      </section>
      <ButtonsSection />
      <InputsSection />
      <PickersSection />
      <NotifySection />
      <TagsSection />
      <FeedbackSection />
      <TimetableSection />
      <ImageSection />
      <ListSection />
    </div>
  );
}

function Section({ id, title, description, children }: { id?: string; title: string; description?: string; children: ReactNode }) {
  return (
    <section id={id} className="scroll-mt-20 rounded-[var(--radius-card)] border border-line bg-surface p-4 shadow-[var(--shadow-soft-sm)] sm:p-6">
      <h2 className="text-xl font-semibold leading-7">{title}</h2>
      {description && <p className="mt-1 text-[13px] text-ink-muted">{description}</p>}
      <div className="mt-4">{children}</div>
    </section>
  );
}

function ButtonsSection() {
  const [loading, setLoading] = useState(false);
  const variants: ButtonVariant[] = ['solid', 'soft', 'outline', 'ghost', 'link'];
  return (
    <Section id="buttons" title="Button" description="Màu & icon tự gán theo chức năng (prop `action`). Icon là bắt buộc.">
      <div className="flex flex-col gap-5">
        <div className="flex flex-wrap gap-2">
          {(Object.keys(BUTTON_ACTIONS) as ButtonAction[]).map((a) => (
            <Button key={a} action={a} />
          ))}
        </div>

        <div className="flex flex-col gap-2">
          <p className="text-[13px] font-medium text-ink-secondary">Các kiểu (variant) của nút Xóa</p>
          <div className="flex flex-wrap gap-2">
            {variants.map((v) => (
              <Button key={v} action="delete" variant={v}>
                {v}
              </Button>
            ))}
          </div>
        </div>

        <div className="flex flex-col gap-2">
          <p className="text-[13px] font-medium text-ink-secondary">Kích thước · loading · chỉ icon · icon tùy chỉnh</p>
          <div className="flex flex-wrap items-center gap-2">
            <Button action="save" size="sm" />
            <Button action="save" size="md" />
            <Button action="save" size="lg" />
            <Button
              action="send"
              loading={loading}
              onClick={() => {
                setLoading(true);
                setTimeout(() => setLoading(false), 1500);
              }}
            >
              Gửi thông báo
            </Button>
            <Button action="edit" iconOnly />
            <Button action="delete" iconOnly />
            <Button action="view" iconOnly />
            <Button icon={Star} tone="warning" variant="outline">
              Đánh dấu
            </Button>
            <Button action="create" disabled />
          </div>
        </div>

        <div className="flex flex-col gap-2">
          <p className="text-[13px] font-medium text-ink-secondary">
            collapseOnMobile — màn &lt; 640px chỉ còn icon
          </p>
          <div className="flex flex-wrap gap-2">
            <Button action="create" collapseOnMobile>
              Thêm học viên
            </Button>
            <Button action="export" collapseOnMobile />
            <Button action="print" collapseOnMobile />
          </div>
        </div>
      </div>
    </Section>
  );
}

function InputsSection() {
  const { message } = App.useApp();
  const [form] = Form.useForm();
  const [fee, setFee] = useState<number | null>(4500000);
  return (
    <Section id="inputs" title="Input" description="Dùng độc lập (có label/hint/error) hoặc trong Form.Item của AntD.">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <TextInput label="Họ và tên" required icon={User} placeholder="Nguyễn Văn A" />
        <TextInput label="Email" type="email" icon={Mail} placeholder="email@example.com" hint="Dùng để đăng nhập" />
        <TextInput label="Số điện thoại" type="tel" icon={Phone} defaultValue="09123" error="Số điện thoại chưa đúng" />
        <TextInput label="Mật khẩu" type="password" required />
        <NumberInput
          label="Học phí"
          currency
          value={fee}
          onChange={setFee}
          hint={fee ? `Đọc: ${formatCurrency(fee)}` : undefined}
        />
        <NumberInput label="Số buổi" unit="buổi" min={1} max={200} defaultValue={48} />
        <TextArea label="Ghi chú tư vấn" maxLength={500} placeholder="Nhu cầu, trình độ, thời gian rảnh..." fieldClassName="md:col-span-2" />
        <TextInput label="Bị vô hiệu" disabled defaultValue="Không sửa được" />
      </div>

      <div className="mt-6 border-t border-line pt-6">
        <p className="mb-3 text-[13px] font-medium text-ink-secondary">Bên trong Form AntD (có validate)</p>
        <Form
          form={form}
          layout="vertical"
          requiredMark
          onFinish={(v) => message.success(`Đã lưu: ${JSON.stringify(v)}`)}
          className="grid grid-cols-1 gap-x-4 md:grid-cols-2"
        >
          <Form.Item name="name" label="Tên khóa học" rules={[{ required: true, message: 'Nhập tên khóa học' }]}>
            <TextInput placeholder="JLPT N3 cấp tốc" />
          </Form.Item>
          <Form.Item name="price" label="Học phí" rules={[{ required: true, message: 'Nhập học phí' }]}>
            <NumberInput currency />
          </Form.Item>
          <Form.Item name="description" label="Mô tả" className="md:col-span-2">
            <TextArea maxLength={300} />
          </Form.Item>
          <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end md:col-span-2">
            <Button action="cancel" onClick={() => form.resetFields()} />
            <Button action="save" htmlType="submit">
              Lưu khóa học
            </Button>
          </div>
        </Form>
      </div>
    </Section>
  );
}

/** Ảnh mẫu nhúng sẵn (không cần mạng) */
const DEMO_COVER =
  'data:image/svg+xml;utf8,' +
  encodeURIComponent(
    '<svg xmlns="http://www.w3.org/2000/svg" width="800" height="450"><defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#EAF4FF"/><stop offset="1" stop-color="#A3CBF5"/></linearGradient></defs><rect width="800" height="450" fill="url(#g)"/><circle cx="400" cy="200" r="90" fill="#1F6FC5" opacity=".85"/><text x="400" y="360" font-family="sans-serif" font-size="40" text-anchor="middle" fill="#143D6B">日本語 · JLPT N3</text></svg>',
  );

function ImageSection() {
  const [avatar, setAvatar] = useState<string | null>(null);
  const [cover, setCover] = useState<string | null>(DEMO_COVER);
  const [logo, setLogo] = useState<string | null>(null);
  return (
    <Section id="images" title="ImagePicker" description="Bấm, kéo-thả hoặc Enter để chọn. Kiểm tra định dạng và dung lượng.">
      <div className="flex flex-col gap-6 md:flex-row md:flex-wrap">
        <ImagePicker label="Ảnh đại diện" shape="circle" value={avatar} onChange={(v) => setAvatar(v)} />
        <ImagePicker label="Logo chi nhánh" value={logo} onChange={(v) => setLogo(v)} maxSizeMB={1} required />
        <ImagePicker
          label="Ảnh bìa khóa học"
          shape="wide"
          value={cover}
          onChange={(v) => setCover(v)}
          className="md:max-w-md md:flex-1"
        />
      </div>
    </Section>
  );
}

const SAMPLE = Array.from({ length: 137 }, (_, i) => ({
  id: i + 1,
  name: `Học viên ${String(i + 1).padStart(3, '0')}`,
  level: ['N5', 'N4', 'N3', 'N2', 'N1'][i % 5],
  branch: ['Cầu Giấy', 'Quận 1', 'Hải Châu'][i % 3],
}));

function ListSection() {
  const [keyword, setKeyword] = useState('');
  const [level, setLevel] = useState<string>();
  const [branch, setBranch] = useState<string>();
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const filtered = useMemo(
    () =>
      SAMPLE.filter(
        (s) =>
          s.name.toLowerCase().includes(keyword.toLowerCase()) &&
          (!level || s.level === level) &&
          (!branch || s.branch === branch),
      ),
    [keyword, level, branch],
  );
  const rows = filtered.slice((page - 1) * pageSize, page * pageSize);

  return (
    <Section id="list" title="SearchBar + Pagination" description="Gõ để tìm (debounce 400ms). Trên mobile, bộ lọc gom vào nút.">
      <SearchBar
        value={keyword}
        onSearch={(k) => {
          setKeyword(k);
          setPage(1);
        }}
        placeholder="Tìm theo tên học viên..."
        activeFilterCount={[level, branch].filter(Boolean).length}
        filters={
          <>
            <Select
              allowClear
              placeholder="Trình độ"
              value={level}
              onChange={(v) => {
                setLevel(v);
                setPage(1);
              }}
              options={['N5', 'N4', 'N3', 'N2', 'N1'].map((v) => ({ value: v, label: v }))}
              className="w-full lg:w-36"
            />
            <Select
              allowClear
              placeholder="Chi nhánh"
              value={branch}
              onChange={(v) => {
                setBranch(v);
                setPage(1);
              }}
              options={['Cầu Giấy', 'Quận 1', 'Hải Châu'].map((v) => ({ value: v, label: v }))}
              className="w-full lg:w-40"
            />
          </>
        }
        onReset={() => {
          setKeyword('');
          setLevel(undefined);
          setBranch(undefined);
          setPage(1);
        }}
        actions={
          <>
            <Button action="export" collapseOnMobile />
            <Button action="create" collapseOnMobile>
              Thêm học viên
            </Button>
          </>
        }
      />

      <ul className="mt-4 divide-y divide-line rounded-[var(--radius-card)] border border-line">
        {rows.length === 0 && <li className="p-6 text-center text-ink-muted">Không có kết quả phù hợp</li>}
        {rows.map((r) => (
          <li key={r.id} className="flex items-center justify-between gap-3 px-4 py-3 hover:bg-primary-50">
            <div className="min-w-0">
              <p className="truncate font-medium">{r.name}</p>
              <p className="text-[13px] text-ink-muted">
                {r.branch} · {r.level}
              </p>
            </div>
            <div className="flex shrink-0 gap-1">
              <Button action="view" iconOnly size="sm" />
              <Button action="edit" iconOnly size="sm" />
              <Button action="delete" iconOnly size="sm" />
            </div>
          </li>
        ))}
      </ul>

      <Pagination
        page={page}
        pageSize={pageSize}
        total={filtered.length}
        unitLabel="học viên"
        onChange={(p, s) => {
          setPage(s !== pageSize ? 1 : p);
          setPageSize(s);
        }}
      />
    </Section>
  );
}
