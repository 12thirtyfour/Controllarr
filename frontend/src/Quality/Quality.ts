export enum QualitySource {
  Unknown = 'unknown',
  Prototype = 'prototype',
  Patch = 'patch',
  Unverified = 'unverified',
  Verified = 'verified',
  NoIntro = 'noIntro',
  Redump = 'redump',
}

export interface Revision {
  version: number;
  real: number;
  isRepack: boolean;
}

interface Quality {
  id: number;
  name: string;
  resolution: number;
  source: QualitySource;
  minSize: number | null;
  maxSize: number | null;
  preferredSize: number | null;
}

export interface QualityModel {
  quality: Quality;
  revision: Revision;
}

export default Quality;
