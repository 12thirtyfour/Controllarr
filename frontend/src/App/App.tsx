import { QueryClientProvider } from '@tanstack/react-query';
import React, { Suspense } from 'react';
import {
  createBrowserRouter,
  createRoutesFromElements,
  Outlet,
  Route,
  RouterProvider,
} from 'react-router-dom';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Page from 'Components/Page/Page';
import RouteErrorPage from 'Components/Error/RouteErrorPage';
import ApplyTheme from './ApplyTheme';
import { appRouteElements } from './AppRoutes';
import { queryClient } from './queryClient';

function PageLayout() {
  return (
    <>
      <ApplyTheme />
      <Page>
        <Suspense fallback={<LoadingIndicator />}>
          <Outlet />
        </Suspense>
      </Page>
    </>
  );
}

const router = createBrowserRouter(
  createRoutesFromElements(
    <Route element={<PageLayout />} errorElement={<RouteErrorPage />}>
      {appRouteElements()}
    </Route>
  ),
  { basename: window.Controllarr.urlBase || undefined }
);

function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  );
}

export default App;
