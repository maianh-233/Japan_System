import { useMemo } from 'react';
import { useSystemStore } from './systemStore';
import type { BranchProfile } from './types';

export function getBranches(includeNonActive = true): BranchProfile[] {
  const branches = useSystemStore.getState().branches;
  return includeNonActive ? branches : branches.filter((branch) => branch.status === 'active');
}

export function useBranches(includeNonActive = true): BranchProfile[] {
  const branches = useSystemStore((state) => state.branches);
  return useMemo(() => (includeNonActive ? branches : branches.filter((branch) => branch.status === 'active')), [branches, includeNonActive]);
}
