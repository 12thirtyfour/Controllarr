type Populator = () => void;

const REPOPULATE_DEBOUNCE_MS = 250;

let currentPopulator: Populator | null = null;
let currentReasons: string[] = [];
let repopulateTimer: ReturnType<typeof setTimeout> | null = null;

function clearRepopulateTimer() {
  if (repopulateTimer !== null) {
    clearTimeout(repopulateTimer);
    repopulateTimer = null;
  }
}

export function registerPagePopulator(
  populator: Populator,
  reasons: string[] = []
) {
  clearRepopulateTimer();
  currentPopulator = populator;
  currentReasons = reasons;
}

export function unregisterPagePopulator(populator: Populator) {
  if (currentPopulator === populator) {
    clearRepopulateTimer();
    currentPopulator = null;
    currentReasons = [];
  }
}

export function repopulatePage(reason?: string) {
  if (!currentPopulator) {
    return;
  }

  if (reason && !currentReasons.includes(reason)) {
    return;
  }

  clearRepopulateTimer();
  repopulateTimer = setTimeout(() => {
    repopulateTimer = null;
    currentPopulator?.();
  }, REPOPULATE_DEBOUNCE_MS);
}
