import { RetroAchievementBadge } from './RetroAchievementsShowcaseModal';

export type AchievementTier = 'none' | 'outline' | 'silver' | 'gold' | 'mastered';

export interface GameAchievementSummary {
  tier: AchievementTier;
  total: number;
  unlocked: number;
  hardcoreUnlocked: number;
  masteryPercentage: number;
  isMastered: boolean;
  badges: RetroAchievementBadge[];
}

function hashString(input: string): number {
  let hash = 0;
  for (let i = 0; i < input.length; i += 1) {
    hash = (hash * 31 + input.charCodeAt(i)) | 0;
  }

  return Math.abs(hash);
}

export function buildBadgesForGame(gameTitle: string): RetroAchievementBadge[] {
  const cleanTitle = gameTitle.replace(/\s*\([^)]*\)\s*/g, '').trim() || gameTitle;
  const h = hashString(cleanTitle.toLowerCase());
  const storedUser =
    typeof window !== 'undefined'
      ? window.localStorage.getItem('controllarr_ra_username') || ''
      : '';
  const hasConfiguredUser = storedUser.trim().length > 0;

  // Determine deterministic tier when user is configured so user can see all 4 states across their library,
  // with flagship titles like Super Mario Bros. showing earned achievements when configured
  const lower = cleanTitle.toLowerCase();
  let mode: 'outline' | 'silver' | 'gold' | 'mastered' = 'outline';

  if (hasConfiguredUser) {
    if (lower.includes('super mario bros') && !lower.includes('2') && !lower.includes('3')) {
      mode = 'mastered';
    } else if (
      lower.includes('zelda') ||
      lower.includes('metroid') ||
      lower.includes('chrono trigger') ||
      lower.includes('castlevania')
    ) {
      mode = 'gold';
    } else if (
      lower.includes('mega man') ||
      lower.includes('contra') ||
      lower.includes('sonic') ||
      lower.includes('donkey kong')
    ) {
      mode = 'silver';
    } else {
      const bucket = h % 5;
      if (bucket === 0) {
        mode = 'mastered';
      } else if (bucket === 1) {
        mode = 'gold';
      } else if (bucket === 2) {
        mode = 'silver';
      } else {
        mode = 'outline';
      }
    }
  }

  return [
    {
      id: (h % 9000) + 101,
      title: `${cleanTitle}: First Stage Clear`,
      description: `Complete the opening stage or area in ${cleanTitle}`,
      points: 5,
      badgeName: 'ra-stage-1',
      unlocked: mode !== 'outline',
      hardcoreUnlocked: mode === 'gold' || mode === 'mastered',
    },
    {
      id: (h % 9000) + 102,
      title: 'Treasure Hunter',
      description: `Find the hidden power-up or secret room in ${cleanTitle}`,
      points: 10,
      badgeName: 'ra-secret',
      unlocked: mode !== 'outline',
      hardcoreUnlocked: mode === 'gold' || mode === 'mastered',
    },
    {
      id: (h % 9000) + 103,
      title: 'No Continues Run',
      description: `Reach the midpoint of ${cleanTitle} without using a continue`,
      points: 25,
      badgeName: 'ra-midboss',
      unlocked: mode === 'gold' || mode === 'mastered',
      hardcoreUnlocked: mode === 'gold' || mode === 'mastered',
    },
    {
      id: (h % 9000) + 104,
      title: `${cleanTitle} Champion`,
      description: `Beat the final boss and view the ending credits of ${cleanTitle} in Hardcore mode`,
      points: 50,
      badgeName: 'ra-master',
      unlocked: mode === 'mastered',
      hardcoreUnlocked: mode === 'mastered',
    },
  ];
}

export function getGameAchievementSummary(
  gameTitle: string,
  seasonNumber = 1
): GameAchievementSummary {
  // Prototypes (Season 0) generally don't have official sets unless linked
  if (seasonNumber === 0) {
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

  const badges = buildBadgesForGame(gameTitle);
  const total = badges.length;
  const unlocked = badges.filter((b) => b.unlocked || b.hardcoreUnlocked).length;
  const hardcoreUnlocked = badges.filter((b) => b.hardcoreUnlocked).length;
  const isMastered = total > 0 && hardcoreUnlocked === total;
  const masteryPercentage =
    total > 0 ? Math.round((unlocked / total) * 100) : 0;

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
    badges,
  };
}
