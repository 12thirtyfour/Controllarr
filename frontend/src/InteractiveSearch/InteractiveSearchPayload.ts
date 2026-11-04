interface GameSearchPayload {
  gameId: number;
}

interface RegionSearchPayload {
  seriesId: number;
  seasonNumber: number;
}

type InteractiveSearchPayload = GameSearchPayload | RegionSearchPayload;

export default InteractiveSearchPayload;
