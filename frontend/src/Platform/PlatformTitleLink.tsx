import React from 'react';
import Link, { LinkProps } from 'Components/Link/Link';

export interface PlatformTitleLinkProps extends LinkProps {
  titleSlug: string;
  title: string;
}

export default function PlatformTitleLink({
  titleSlug,
  title,
  ...linkProps
}: PlatformTitleLinkProps) {
  const link = `/platforms/${titleSlug}`;

  return (
    <Link to={link} {...linkProps}>
      {title}
    </Link>
  );
}
