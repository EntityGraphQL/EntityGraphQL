const config = {
  gatsby: {
    pathPrefix: '/',
    siteUrl: 'https://github.com/lukemurray/EntityGraphQL',
    gaTrackingId: null,
    trailingSlash: false,
  },
  header: {
    logo: '',
    logoLink: null,
    title: "Entity GraphQL",
    githubUrl: 'https://github.com/lukemurray/EntityGraphQL',
    helpUrl: '',
    tweetText: '',
    social: ``,
    links: [{ text: '', link: '' }],
    search: {
      enabled: false,
      indexName: '',
      algoliaAppId: process.env.GATSBY_ALGOLIA_APP_ID,
      algoliaSearchKey: process.env.GATSBY_ALGOLIA_SEARCH_KEY,
      algoliaAdminKey: process.env.ALGOLIA_ADMIN_KEY,
    },
  },
  sidebar: {
    forcedNavOrder: [
      '/introduction',
      '/getting-started',
      '/schema-creation',
      '/schema-helpers',
      '/authorization',
      '/validation',
      '/other-data-sources',
      '/entity-framework',
      '/how-it-works',
    ],
    collapsedNav: [],
    links: [],
    frontline: false,
    ignoreIndex: true,
    title: "Documentation",
  },
  siteMetadata: {
    title: 'Entity GraphQL Docs',
    description: 'Documentation for Entity GraphQL - a .NET Core GraphQL library',
    ogImage: null,
    docsLocation: 'https://github.com/lukemurray/EntityGraphQL/tree/master/docs/content',
    favicon: '',
  },
  pwa: {
    enabled: false, // disabling this will also remove the existing service worker.
    manifest: {
      name: 'Gatsby Gitbook Starter',
      short_name: 'GitbookStarter',
      start_url: '/',
      background_color: '#6b37bf',
      theme_color: '#6b37bf',
      display: 'standalone',
      crossOrigin: 'use-credentials',
      icons: [
        {
          src: 'src/pwa-512.png',
          sizes: `512x512`,
          type: `image/png`,
        },
      ],
    },
  },
};

module.exports = config;
