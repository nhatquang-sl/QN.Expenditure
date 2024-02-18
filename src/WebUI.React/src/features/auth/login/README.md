# Zod 
- `npm install zod`
- Zod provide TypeScript-first schema validation with static type inference
  - Provide a schema describes the structure of your data and that's kind of also what we do with TypeScript, but here's the difference
    - TypeScript helps at the data type level during development, your code is compiled of course after that and that leaves a blind spot. What about the runtime after the code is compiled? Sometimes you can receive unexpected data that can come from a form with user data, it can come from an API or even a database. 
    - But Zod lets you apply runtime type checking.
    - `static type inference` so we can infer our types from our Zod schema

# react-hook-form
`npm install react-hook-form @hookform/resolvers`

# reduxjs/toolkit
`npm install @reduxjs/toolkit react-redux`  
The Redux Toolkit package is intended to be the standard way to write Redux logic. It was originally created to help address three common concerns about Redux:

- "Configuring a Redux store is too complicated"
- "I have to add a lot of packages to get Redux to do anything useful"
- "Redux requires too much boilerplate code"

## Slice
- Slice come from splitting up redux state object into multiple slices of state. So a slice is really a collection of reducer logic and actions for a single feature in the app.
e.g a blog might have a slice for posts and another slice for comments. You would handle the logic of each differently so they each get their own slice. 