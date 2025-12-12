import { Dayjs } from 'dayjs';
import { InputElement } from '../types';

export class DateTimeElement extends InputElement {
  constructor(
    label: string,
    defaultValue?: Dayjs,
    disabled: boolean = false,
    flex: number | 'none' = 1
  ) {
    super('datetime', label, defaultValue, disabled, flex);
  }
}
