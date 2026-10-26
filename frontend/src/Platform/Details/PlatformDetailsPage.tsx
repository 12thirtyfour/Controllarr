import React, { useEffect } from 'react';
import { useNavigate, useParams } from 'react-router';
import NotFound from 'Components/NotFound';
import usePrevious from 'Helpers/Hooks/usePrevious';
import usePlatform from 'Platform/usePlatform';
import translate from 'Utilities/String/translate';
import PlatformDetails from './PlatformDetails';

function PlatformDetailsPage() {
  const { data: allSeries } = usePlatform();
  const { titleSlug } = useParams<{ titleSlug: string }>();
  const navigate = useNavigate();

  const series = allSeries.find((series) => series.titleSlug === titleSlug);
  const seriesId = series?.id;
  const previousSeriesId = usePrevious(seriesId);
  const previousTitleSlug = usePrevious(titleSlug);

  useEffect(() => {
    if (
      seriesId !== undefined ||
      previousSeriesId === undefined ||
      titleSlug !== previousTitleSlug
    ) {
      return;
    }

    const renamedSeries = allSeries.find(
      (series) => series.id === previousSeriesId
    );

    if (renamedSeries) {
      navigate(`/platforms/${renamedSeries.titleSlug}`, { replace: true });
    } else {
      navigate('/');
    }
  }, [
    allSeries,
    seriesId,
    previousSeriesId,
    titleSlug,
    previousTitleSlug,
    navigate,
  ]);

  if (!series) {
    return <NotFound message={translate('SeriesCannotBeFound')} />;
  }

  return <PlatformDetails seriesId={series.id} />;
}

export default PlatformDetailsPage;
