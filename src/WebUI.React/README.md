# React + TypeScript + Vite

This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react/README.md) uses [Babel](https://babeljs.io/) for Fast Refresh
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react-swc) uses [SWC](https://swc.rs/) for Fast Refresh

## Expanding the ESLint configuration

If you are developing a production application, we recommend updating the configuration to enable type aware lint rules:

- Configure the top-level `parserOptions` property like this:

```js
   parserOptions: {
    ecmaVersion: 'latest',
    sourceType: 'module',
    project: ['./tsconfig.json', './tsconfig.node.json'],
    tsconfigRootDir: __dirname,
   },
```

- Replace `plugin:@typescript-eslint/recommended` to `plugin:@typescript-eslint/recommended-type-checked` or `plugin:@typescript-eslint/strict-type-checked`
- Optionally add `plugin:@typescript-eslint/stylistic-type-checked`
- Install [eslint-plugin-react](https://github.com/jsx-eslint/eslint-plugin-react) and add `plugin:react/recommended` & `plugin:react/jsx-runtime` to the `extends` list

# Tailwind CSS
- `npx tailwindcss init`: create tailwind.config.js

## Set up tailwind.config.js
- We need to tell Tailwind where our HTML is so that it can get the classes that we are using, to include in the style file that we will create with Tailwind.
```javascript
content: ['./index.html'],
```

## Usage
- We need to include the following code in the beginning of css files that we want to use tailwind
```css
@tailwind base;
@tailwind components;
@tailwind utilities;
```

## VSCode
- To disable warning `@tailwind` in css file, go to setting then ignore `CSS>Lints: Unknown At Rules`
- Install `Tailwind CSS IntelliSense` from extensions.
- `npm i prettier-plugin-tailwindcss -D`: Tailwind has provided plug-in for class order.
  ```json
   "scripts": {
      // look for all HTML files then format them.
      "prettier": "npx prettier --write **/*.html"
   }
  ```

## Build
- `npx tailwind -i {input_css_file} -o {output_css_file}`: to compile tailwind css to pure css