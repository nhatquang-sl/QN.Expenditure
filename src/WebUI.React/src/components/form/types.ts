import { MouseEventHandler } from 'react';

export class Block {
  constructor(elements: InputElement[] = [], actions: ActionElement[] = []) {
    this.elements = elements;
    this.actions = actions;
    this.id = makeId();
  }

  id: string;
  elements: InputElement[];
  actions: ActionElement[];
}

export class ActionBlock extends Block {
  constructor(actions: ActionElement[]) {
    super([], actions);
  }
}

function makeId(length: number = 5) {
  let result = '';
  const characters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
  const charactersLength = characters.length;
  let counter = 0;
  while (counter < length) {
    result += characters.charAt(Math.floor(Math.random() * charactersLength));
    counter += 1;
  }
  return result;
}

export class InputElement {
  constructor(
    type: 'text' | 'select' | 'number' | 'compute',
    label: string,
    defaultValue: unknown = null,
    flex: number | 'none' = 1
  ) {
    this.type = type;
    this.label = label;
    this.flex = flex;
    this.defaultValue = defaultValue;
  }
  type: 'text' | 'select' | 'number' | 'compute' = 'text';
  label: string = '';
  flex: number | 'none';
  defaultValue?: unknown = null;
  options: InputOption[] = [];
  computedValue?: (getValues: (fieldId: string) => string) => string;
}

export class SelectElement extends InputElement {
  constructor(
    label: string,
    defaultValue?: unknown,
    options: InputOption[] = [],
    flex: number | 'none' = 1
  ) {
    super('select', label, defaultValue, flex);
    this.options = options;
  }
}

export class NumberElement extends InputElement {
  constructor(label: string, defaultValue?: unknown, flex: number | 'none' = 1) {
    super('number', label, defaultValue, flex);
  }
}

export class InputOption {
  constructor(value: string | number, label: string | undefined = undefined) {
    this.value = value;
    if (label === undefined) this.label = value;
    else this.label = label;
  }
  label: string | number;
  value: string | number;
}

export class ActionElement {
  constructor(
    label: string,
    type: 'button' | 'submit' = 'button',
    onClickButton: MouseEventHandler<HTMLButtonElement> | undefined = undefined
  ) {
    this.label = label;
    this.type = type;
    this.onClickButton = onClickButton;
  }
  label: string = '';
  type: 'button' | 'submit';
  onClickButton: React.MouseEventHandler<HTMLButtonElement> | undefined;
}
