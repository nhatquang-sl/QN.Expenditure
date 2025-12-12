import { InputElement, InputOption } from '../types';

export class SelectElement extends InputElement {
  constructor(
    label: string,
    defaultValue?: unknown,
    options: InputOption[] = [],
    disabled: boolean = false
  ) {
    super('select', label, defaultValue, disabled);
    this.options = options;
  }
}
