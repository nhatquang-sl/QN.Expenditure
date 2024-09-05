// console.log(import.meta.env);
export const sidebarWidth = 240;
export const API_ENDPOINT = 'http://localhost:5229'; // 'https://quangnn.somee.com'; // import.meta.env.DEV ? 'http://localhost:5228' : '';

export const PAGE = {
  START: 1,
  SIZE: 10,
};

// Number.EPSILON in case round 1.005 => 1.01
const round2Dec = (num: string | number) =>
  Math.round((parseFloat(`${num}`) + Number.EPSILON) * 100) / 100;

const round3Dec = (num: string | number) =>
  Math.round((parseFloat(`${num}`) + Number.EPSILON) * 1000) / 1000;

const round4Dec = (num: string | number) =>
  Math.round((parseFloat(`${num}`) + Number.EPSILON) * 10000) / 10000;

export { round2Dec, round3Dec, round4Dec };
