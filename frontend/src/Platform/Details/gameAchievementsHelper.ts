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

const libraryItems: RetroAchievementsGameProgress[] = [];
let loadedUserKey = '';
let inFlightPromise: Promise<void> | null = null;
let cacheVersion = 0;
const listeners = new Set<() => void>();

export function resolveConsoleKey(consoleName?: string): string {
  if (!consoleName) {
    return '';
  }

  const c = consoleName.toLowerCase();
  if (
    c.includes('super nintendo') ||
    c.includes('super famicom') ||
    c === 'sfc' ||
    c.includes('snes')
  ) {
    return 'snes';
  }

  if (
    c.includes('nintendo entertainment system') ||
    c.includes('famicom') ||
    c === 'fc' ||
    c.includes('nes')
  ) {
    return 'nes';
  }

  if (c.includes('nintendo 64') || c.includes('n64')) {
    return 'n64';
  }

  if (c.includes('game boy advance') || c.includes('gba')) {
    return 'gba';
  }

  if (c.includes('game boy color') || c.includes('gbc')) {
    return 'gbc';
  }

  if (c.includes('game boy') || c.includes('dmg') || c === 'gb') {
    return 'gb';
  }

  if (c.includes('nintendo ds') || c.includes('nds')) {
    return 'nds';
  }

  if (c.includes('playstation 2') || c.includes('ps2')) {
    return 'ps2';
  }

  if (c.includes('playstation portable') || c.includes('psp')) {
    return 'psp';
  }

  if (c.includes('playstation') || c.includes('ps1') || c.includes('psx')) {
    return 'ps1';
  }

  if (c.includes('sega cd') || c.includes('mega cd') || c.includes('segacd')) {
    return 'segacd';
  }

  if (c.includes('saturn')) {
    return 'saturn';
  }

  if (c.includes('dreamcast')) {
    return 'dreamcast';
  }

  if (c.includes('master system') || c === 'sms') {
    return 'sms';
  }

  if (c.includes('genesis') || c.includes('mega drive') || c === 'md') {
    return 'genesis';
  }

  if (c.includes('pc engine') || c.includes('turbografx')) {
    return 'tg16';
  }

  if (c.includes('2600')) {
    return 'atari2600';
  }

  if (c.includes('7800')) {
    return 'atari7800';
  }

  if (c.includes('lynx')) {
    return 'lynx';
  }

  if (c.includes('jaguar')) {
    return 'jaguar';
  }

  return c.replace(/[^a-z0-9]/g, '');
}

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
  libraryItems.length = 0;
  notifyListeners();
}

export function findMatchingGameProgress(
  gameTitle: string,
  consoleName?: string
): RetroAchievementsGameProgress | undefined {
  const normTitle = normalizeGameTitle(gameTitle);
  if (!normTitle) {
    return undefined;
  }

  const targetConsoleKey = resolveConsoleKey(consoleName);

  if (targetConsoleKey) {
    const exactMatch = libraryItems.find(
      (item) =>
        normalizeGameTitle(item.gameTitle) === normTitle &&
        resolveConsoleKey(item.consoleName) === targetConsoleKey
    );
    if (exactMatch) {
      return exactMatch;
    }

    return undefined;
  }

  return libraryItems.find(
    (item) => normalizeGameTitle(item.gameTitle) === normTitle
  );
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
      libraryItems.length = 0;
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
      libraryItems.length = 0;

      if (Array.isArray(items)) {
        libraryItems.push(...items);
      }

      notifyListeners();
    })
    .catch(() => {
      // Keep empty array on network error so fake badges are never shown
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

export function buildBadgesForGame(
  gameTitle: string,
  consoleName?: string
): RetroAchievementBadge[] {
  ensureLibraryLoaded();
  const matched = findMatchingGameProgress(gameTitle, consoleName);

  return matched?.badges ?? [];
}

export function getGameAchievementSummary(
  gameTitle: string,
  seasonNumber = 1,
  _cacheVersion = 0,
  consoleName?: string
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
  const matched = findMatchingGameProgress(gameTitle, consoleName);

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
