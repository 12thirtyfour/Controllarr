import React, { useCallback } from 'react';
import { useRouteError } from 'react-router-dom';
import Button from 'Components/Link/Button';
import Page from 'Components/Page/Page';
import PageContentBody from 'Components/Page/PageContentBody';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import ErrorBoundaryError from './ErrorBoundaryError';

function RouteErrorPage() {
  const error = useRouteError();
  const errorObj =
    error instanceof Error ? error : new Error(String(error || 'Route error'));

  const handleReload = useCallback(() => {
    window.location.reload();
  }, []);

  const handleHome = useCallback(() => {
    window.location.href = `${window.Controllarr.urlBase}/`;
  }, []);

  return (
    <Page>
      <PageContentBody>
        <ErrorBoundaryError
          className=""
          messageClassName=""
          detailsClassName=""
          message={translate('ErrorLoadingPage')}
          error={errorObj}
          info={{ componentStack: '' }}
        />
        <div
          style={{
            display: 'flex',
            gap: '12px',
            justifyContent: 'center',
            marginTop: '20px',
          }}
        >
          <Button kind={kinds.PRIMARY} onPress={handleReload}>
            Reload Application
          </Button>
          <Button kind={kinds.DEFAULT} onPress={handleHome}>
            Go to Platforms
          </Button>
        </div>
      </PageContentBody>
    </Page>
  );
}

export default RouteErrorPage;
