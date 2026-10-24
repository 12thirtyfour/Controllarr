import { useEffect, useState } from 'react';
import {
  RetroAchievementBadge,
  RetroAchievementsGameProgress,
} from './RetroAchievementsShowcaseModal';

export type AchievementTier =
  | 'none'
  | 'outline'
  | 'silver'
  | 'gold'
  | 'mastered';

export interface GameAchievementSummary {
  tier: AchievementTier;
  total: number;
  unlocked: number;
  hardcoreUnlocked: number;
  masteryPercentage: number;
  isMastered: boolean;
  badges: RetroAchievementBadge[];
}

const libraryByNormalizedTitle = new Map<
  string,
  RetroAchievementsGameProgress
>();
let loadedUserKey = '';
let inFlightPromise: Promise<void> | null = null;
let cacheVersion = 0;
const listeners = new Set<() => void>();

export function normalizeGameTitle(title: string): string {
  if (!title) {
    return '';
  }

  return title
    .replace(/\s*\([^)]*\)|\s*\[[^\]]*\]/g, ' ')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '');
}

function notifyListeners() {
  cacheVersion += 1;
  listeners.forEach((listener) => listener());
}

export function invalidateRetroAchievementsCache(): void {
  loadedUserKey = '';
  inFlightPromise = null;
  libraryByNormalizedTitle.clear();
  notifyListeners();
}

function ensureLibraryLoaded(): void {
  if (typeof window === 'undefined') {
    return;
  }

  const username = (
    window.localStorage.getItem('controllarr_ra_username') || ''
  ).trim();
  const apiKey = (
    window.localStorage.getItem('controllarr_ra_apikey') || ''
  ).trim();
  const currentKey = `${username}:${apiKey}`;

  if (!username) {
    if (loadedUserKey !== '') {
      loadedUserKey = '';
      libraryByNormalizedTitle.clear();
      notifyListeners();
    }

    return;
  }

  if (loadedUserKey === currentKey || inFlightPromise) {
    return;
  }

  loadedUserKey = currentKey;
  const apiRoot = window.Controllarr?.apiRoot ?? '/api/v3';
  const appApiKey = window.Controllarr?.apiKey ?? '';
  const query = new URLSearchParams({
    username,
    webApiKey: apiKey,
  });

  inFlightPromise = fetch(
    `${apiRoot}/retroachievements/library?${query.toString()}`,
    {
      headers: appApiKey ? { 'X-Api-Key': appApiKey } : undefined,
    }
  )
    .then(async (response) => {
      if (!response.ok) {
        return;
      }

      const items = (await response.json()) as RetroAchievementsGameProgress[];
      libraryByNormalizedTitle.clear();

      if (Array.isArray(items)) {
        items.forEach((item) => {
          const key = normalizeGameTitle(item.gameTitle);

          if (key) {
            libraryByNormalizedTitle.set(key, item);
          }
        });
      }

      notifyListeners();
    })
    .catch(() => {
      // Keep empty map on network error so fake badges are never shown
    })
    .finally(() => {
      inFlightPromise = null;
    });
}

export function useRetroAchievementsLibraryVersion(): number {
  const [version, setVersion] = useState(cacheVersion);

  useEffect(() => {
    const listener = () => setVersion(cacheVersion);
    listeners.add(listener);
    ensureLibraryLoaded();

    return () => {
      listeners.delete(listener);
    };
  }, []);

  return version;
}

export function buildBadgesForGame(gameTitle: string): RetroAchievementBadge[] {
  ensureLibraryLoaded();
  const matched = libraryByNormalizedTitle.get(normalizeGameTitle(gameTitle));

  return matched?.badges ?? [];
}

export function getGameAchievementSummary(
  gameTitle: string,
  seasonNumber = 1,
  _cacheVersion = 0
): GameAchievementSummary {
  if (seasonNumber === 0 || seasonNumber === 99) {
    return {
      tier: 'none',
      total: 0,
      unlocked: 0,
      hardcoreUnlocked: 0,
      masteryPercentage: 0,
      isMastered: false,
      badges: [],
    };
  }

  ensureLibraryLoaded();
  const matched = libraryByNormalizedTitle.get(normalizeGameTitle(gameTitle));

  if (!matched || matched.totalAchievements <= 0) {
    return {
      tier: 'outline',
      total: 0,
      unlocked: 0,
      hardcoreUnlocked: 0,
      masteryPercentage: 0,
      isMastered: false,
      badges: [],
    };
  }

  const total = matched.totalAchievements;
  const unlocked = matched.unlockedAchievements;
  const hardcoreUnlocked = matched.hardcoreUnlockedAchievements;
  const isMastered =
    matched.isMastered || (total > 0 && hardcoreUnlocked >= total);
  const masteryPercentage =
    matched.masteryPercentage ??
    (total > 0 ? Math.round((unlocked / total) * 100) : 0);

  let tier: AchievementTier = 'outline';

  if (isMastered) {
    tier = 'mastered';
  } else if (hardcoreUnlocked > 0) {
    tier = 'gold';
  } else if (unlocked > 0) {
    tier = 'silver';
  }

  return {
    tier,
    total,
    unlocked,
    hardcoreUnlocked,
    masteryPercentage,
    isMastered,
    badges: matched.badges ?? [],
  };
}
