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
      '/serialization-naming',
      '/authorization',
      '/validation',
      '/field-extensions',
      '/entity-framework',
      '/integration',
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
};

module.exports = config;
