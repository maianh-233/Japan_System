import { USE_API } from '@/lib/api/config';
import { useEffect } from 'react';
import dayjs from 'dayjs';
import { useRoomStore } from '@/features/academic/rooms/roomStore';
import { useHrStore } from '@/features/hr/hrStore';
import { useNotificationStore } from '@/features/notifications/notificationStore';
import { useSystemStore } from '@/features/system/systemStore';
import { courseLabel, saleName } from '../constants';
import { useCrmStore } from '../crmStore';
import { fillTemplate, firstName, missingVars, useMessageStore } from '../messages/messages';
import { APPOINTMENT_KIND, REMIND_BEFORE_MIN, useAppointmentStore } from './appointments';

/**
 * NHẮC LỊCH HẸN trước 1 giờ — chạy mỗi phút (backend sẽ chạy bằng job).
 *  - Lead: gửi Zalo theo mẫu "Nhắc lịch test" / "Nhắc lịch tư vấn" (tự điền giờ, phòng, địa chỉ). Mẫu thiếu dữ liệu → không gửi, báo nhân viên.
 *  - Người tiếp / coi test: thông báo trong hệ thống.
 * Mỗi lịch nhắc 1 lần (remindedAt); đổi giờ hẹn thì nhắc lại.
 */
export function runAppointmentReminders(now = dayjs()): number {
  const store = useAppointmentStore.getState();
  const due = store.appointments.filter((a) => a.status === 'scheduled' && !a.remindedAt && dayjs(a.start).isAfter(now) && dayjs(a.start).diff(now, 'minute') <= REMIND_BEFORE_MIN);
  if (!due.length) return 0;

  const { leads } = useCrmStore.getState();
  const rooms = useRoomStore.getState().rooms;
  const branches = useSystemStore.getState().branches;
  const employees = useHrStore.getState().employees;
  const msg = useMessageStore.getState();
  const notify = useNotificationStore.getState().add;

  due.forEach((a) => {
    const lead = leads.find((l) => l.id === a.leadId);
    const staff = employees.find((e) => e.id === a.staffId);
    const room = rooms.find((r) => r.id === a.roomId);
    const branch = branches.find((b) => b.id === a.branchId);
    const start = dayjs(a.start);
    let sentToLead = false;

    const tpl = msg.templates.find((t) => t.active && t.scenario === (a.kind === 'test' ? 'test_reminder' : 'consult_reminder'));
    if (lead && tpl) {
      const body = fillTemplate(tpl.body, {
        ten: firstName(lead.fullName),
        ho_ten: lead.fullName,
        khoa_hoc: courseLabel(lead.interest) ?? lead.interest,
        sale: staff?.fullName ?? saleName(lead.assigneeId),
        ngay: start.format('dddd DD/MM'),
        gio: start.format('HH:mm'),
        phong: room?.name ?? 'quầy lễ tân',
        chi_nhanh: branch?.name,
        dia_chi: branch?.address,
        hotline: branch?.phone,
      });
      if (!missingVars(body).length) {
        msg.send({ channel: tpl.channel, scenario: tpl.scenario, templateId: tpl.id, toName: lead.fullName, toPhone: lead.phone, leadId: lead.id, body, sentBy: 'Hệ thống', auto: true });
        sentToLead = true;
      }
    }

    store.update(a.id, { remindedAt: now.toISOString() });
    notify('staff', {
      title: `${start.format('HH:mm')} hẹn ${APPOINTMENT_KIND[a.kind].short.toLowerCase()}: ${lead?.fullName ?? a.leadId}`,
      description: `${staff ? `Người tiếp: ${staff.fullName}` : ''}${room ? ` · ${room.name}` : ''} · ${sentToLead ? 'đã nhắc khách qua Zalo' : 'CHƯA nhắc được khách (thiếu mẫu / dữ liệu) — hãy gọi xác nhận'}.`,
      tone: sentToLead ? 'info' : 'warning',
      link: `/admin/crm/appointments?open=${a.id}`,
    });
  });
  return due.length;
}

/** Chạy khi vào cổng nhân viên và mỗi phút sau đó */
export function useAppointmentReminders(intervalMs = 60_000) {
  useEffect(() => {
    if (USE_API) return; // Chế độ API: job server làm (CrmMinuteJob)
    runAppointmentReminders();
    const t = window.setInterval(() => runAppointmentReminders(), intervalMs);
    return () => window.clearInterval(t);
  }, [intervalMs]);
}
