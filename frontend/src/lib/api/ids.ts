import { SEED_IDS } from './seedIds.generated';

/**
 * Đổi Id giữa frontend (Id mock: "cg", "e-huy", "R-SA") và backend (GUID).
 * Dữ liệu mẫu backend sinh GUID cố định từ Id mock → đổi 2 chiều được. Bản ghi mới tạo trên backend
 * (vd vai trò mới) không có Id mock → giữ nguyên GUID, giao diện coi như chuỗi Id bình thường.
 */
const reverse = new Map(Object.entries(SEED_IDS).map(([mock, guid]) => [guid, mock]));

export const toGuid = (id: string): string => SEED_IDS[id] ?? id;

export const fromGuid = (guid: string): string => reverse.get(guid.toLowerCase()) ?? guid;

export const toGuids = (ids: readonly string[]) => ids.map(toGuid);

export const fromGuids = (ids: readonly string[]) => ids.map(fromGuid);
