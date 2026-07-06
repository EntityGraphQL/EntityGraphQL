// @ts-check
// Note: type annotations allow type checking and IDEs autocompletion

const { themes } = require('prism-react-renderer');
const lightCodeTheme = themes.github;
const darkCodeTheme = themes.dracula;

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'Entity GraphQL',
  tagline: 'A modern .NET GraphQL library',
  url: 'https://entitygraphql.github.io',
  baseUrl: '/',
  onBrokenLinks: 'throw',
  markdown: {
    hooks: {
      onBrokenMarkdownLinks: 'warn',
    },
  },
  favicon: 'img/favicon.ico',

  // GitHub pages deployment config.
  // If you aren't using GitHub pages, you don't need these.
  organizationName: 'EntityGraphQL', // Usually your GitHub org/user name.
  projectName: 'EntityGraphQL', // Usually your repo name.

  // Even if you don't use internalization, you can use this field to set useful
  // metadata like html lang. For example, if your site is Chinese, you may want
  // to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  plugins: [
    [
      '@docusaurus/plugin-client-redirects',
      {
        // the site previously served docs under /docs/* with a separate landing page at /.
        // Keep old inbound links working
        redirects: [{ from: ['/docs', '/docs/getting-started', '/intro', '/docs/intro'], to: '/' }],
        createRedirects(existingPath) {
          if (existingPath === '/') return undefined;
          return [`/docs${existingPath}`];
        },
      },
    ],
  ],

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          sidebarPath: require.resolve('./sidebars.js'),
          // docs are the site - no separate landing page. Root '/' is getting-started
          routeBasePath: '/',
          // While 6.0 is in beta, 5.7 (snapshotted from the 5.7.2 tag under versioned_docs) is the
          // default version served at the root and the 6.0 beta docs live under /6.0/ with an
          // "unreleased" banner. When 6.0 goes final: set lastVersion to 'current', remove the
          // current.path so 6.0 docs serve at the root, and re-point the announcement bar
          lastVersion: '5.7',
          versions: {
            current: {
              label: '6.0',
              path: '6.0',
              banner: 'unreleased',
            },
            5.7: {
              label: '5.7',
            },
          },
          editUrl:
            'https://github.com/EntityGraphQL/EntityGraphQL/tree/main/docs',
        },
        theme: {
          customCss: require.resolve('./src/css/custom.css'),
        },
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      announcementBar: {
        id: 'v6-beta',
        content: 'EntityGraphQL 6.0.0-beta9 is available - see the <a href="/6.0/">6.0 beta docs</a> and the <a href="/6.0/upgrade-6-0">upgrade guide</a>.',
        isCloseable: true,
      },
      navbar: {
        title: 'Entity GraphQL',
        logo: {
          alt: 'Entity GraphQL Logo',
          src: 'img/logo.svg',
        },
        items: [
          {
            type: 'doc',
            docId: 'getting-started',
            position: 'left',
            label: 'Documentation',
          },
          {
            type: 'docsVersionDropdown',
            position: 'right',
          },
          {
            href: 'https://github.com/EntityGraphQL/EntityGraphQL',
            label: 'GitHub',
            position: 'right',
          },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Docs',
            items: [
              {
                label: 'Getting started',
                to: '/',
              },
            ],
          },
          // {
          //   title: 'Community',
          //   items: [
          //   ],
          // },
          {
            title: 'More',
            items: [
              {
                label: 'GitHub',
                href: 'https://github.com/EntityGraphQL/EntityGraphQL',
              },
              {
                label: 'Changelog',
                href: 'https://github.com/EntityGraphQL/EntityGraphQL/releases',
              },
            ],
          },
        ],
        copyright: `Copyright © ${new Date().getFullYear()} Entity GraphQL. Built with Docusaurus.`,
      },
      prism: {
        theme: lightCodeTheme,
        darkTheme: darkCodeTheme,
        additionalLanguages: ['csharp', 'graphql'],
      },
      algolia: {
        appId: 'NZVRVDGLPG',
        // Public API key: it is safe to commit it
        apiKey: '1382a55cce60fc56fb5c6f05fb12443e',
        indexName: 'entitygraphql',
        contextualSearch: true,

        // Optional: Algolia search parameters
        searchParameters: {},

        // Optional: path for search page that enabled by default (`false` to disable it)
        searchPagePath: 'search',
        //... other Algolia params
      },
    }),
};

module.exports = config;
