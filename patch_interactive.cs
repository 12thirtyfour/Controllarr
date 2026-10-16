<<<<<<< SEARCH
import { Release, useGrabRelease } from './useReleases';
import styles from './InteractiveSearchRow.module.css';
=======
import { Release, useGrabRelease } from './useReleases';
import { useSaveSeries } from 'Series/useSeries';
import IconButton from 'Components/Link/IconButton';
import classNames from 'classnames';
import Label from 'Components/Label';
import styles from './InteractiveSearchRow.module.css';
>>>>>>> REPLACE
<<<<<<< SEARCH
  const { isGrabbing, isGrabbed, grabError, grabRelease } = useGrabRelease();
=======
  const { isGrabbing, isGrabbed, grabError, grabRelease } = useGrabRelease();
  const { updateSeriesMonitor } = useSaveSeries(false);

  const series = searchPayload?.series;
  const isPinned = series?.pinnedVersion && parsedInfo?.revision === series?.pinnedVersion;

  const handlePinToggle = useCallback(() => {
    if (!series) return;
    updateSeriesMonitor({
      ...series,
      pinnedVersion: isPinned ? null : parsedInfo?.revision,
    });
    // Optimistic UI state is ideally handled inside updateSeriesMonitor via React Query mutation
  }, [series, parsedInfo?.revision, isPinned, updateSeriesMonitor]);
>>>>>>> REPLACE
<<<<<<< SEARCH
        {title}
        <ReleaseSceneIndicator sceneMapping={sceneMapping} />
      </VirtualTableRowCell>
=======
        {title}
        {isPinned && <Label kind="info" className={styles.pinnedLabel}>Pinned</Label>}
        <ReleaseSceneIndicator sceneMapping={sceneMapping} />
      </VirtualTableRowCell>
>>>>>>> REPLACE
<<<<<<< SEARCH
        <Link
          className={styles.manualDownloadContent}
          title={translate('OverrideAndAddToDownloadQueue')}
          onPress={onOverridePress}
        >
          <div className={styles.manualDownloadContent}>
            <Icon
              className={styles.interactiveIcon}
              name={icons.INTERACTIVE}
              size={12}
            />

            <Icon
              className={styles.downloadIcon}
              name={icons.CIRCLE_DOWN}
              size={10}
            />
          </div>
        </Link>
      </VirtualTableRowCell>
=======
        <Link
          className={styles.manualDownloadContent}
          title={translate('OverrideAndAddToDownloadQueue')}
          onPress={onOverridePress}
        >
          <div className={styles.manualDownloadContent}>
            <Icon
              className={styles.interactiveIcon}
              name={icons.INTERACTIVE}
              size={12}
            />

            <Icon
              className={styles.downloadIcon}
              name={icons.CIRCLE_DOWN}
              size={10}
            />
          </div>
        </Link>
        {series && (
          <IconButton
            name={icons.PIN}
            className={classNames(styles.pinButton, {
              [styles.pinned]: isPinned,
            })}
            title={
              isPinned
                ? translate('Unpin release')
                : translate('Pin this release as preferred (1G1R override)')
            }
            iconProps={{
              kind: isPinned ? kinds.WARNING : kinds.DEFAULT
            }}
            onPress={handlePinToggle}
          />
        )}
      </VirtualTableRowCell>
>>>>>>> REPLACE
