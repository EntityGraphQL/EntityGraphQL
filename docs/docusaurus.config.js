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
        // Keep old inbound links working. /intro and /docs/intro are NOT listed here: /intro is a real
        // page (the 5.7 snapshot's Introduction, served at the root version) and createRedirects below
        // already maps /docs/intro -> /intro
        redirects: [{ from: ['/docs', '/docs/getting-started'], to: '/' }],
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
          // 6.x (the working docs under docs/) is the default version served at the root. Minor releases are
          // additive so they share this version - the label stays 6.x rather than tracking 6.0/6.1/6.2.
          // 5.7 (snapshotted from the 5.7.2 tag under versioned_docs) remains browsable at /5.7/.
          // Cut the next snapshot (docs:version 6.x) only when 6.x-stable and next-major docs diverge
          lastVersion: 'current',
          versions: {
            current: {
              label: '6.x',
            },
            5.7: {
              label: '5.7',
              banner: 'unmaintained',
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
        // evergreen pointer rather than release news, so it does not go stale each minor. Keep the id
        // stable - anyone who has dismissed it stays dismissed
        id: 'v6-release',
        content: 'These docs cover EntityGraphQL 6.x. Upgrading from 5.x? See the <a href="/upgrade-6-0">upgrade guide</a>.',
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
