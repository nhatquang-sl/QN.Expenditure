# Zod 
- `npm install zod`
- Zod provide TypeScript-first schema validation with static type inference
  - Provide a schema describes the structure of your data and that's kind of also what we do with TypeScript, but here's the difference
    - TypeScript helps at the data type level during development, your code is compiled of course after that and that leaves a blind spot. What about the runtime after the code is compiled? Sometimes you can receive unexpected data that can come from a form with user data, it can come from an API or even a database. 
    - But Zod lets you apply runtime type checking.
    - `static type inference` so we can infer our types from our Zod schema

# react-hook-form
`npm install react-hook-form @hookform/resolvers`