// JSX typing for the official @material/web custom elements used directly as tags in this app.
// Attributes are typed loosely (the elements reflect properties as attributes at runtime);
// event handling for elements that fire non-standard events is done via onChange/onClick,
// which React forwards as native addEventListener calls for custom-element tag names.
import type { DetailedHTMLProps, HTMLAttributes } from 'react'

type MdElementProps = DetailedHTMLProps<HTMLAttributes<HTMLElement>, HTMLElement> & {
  disabled?: boolean
  href?: string
  target?: string
  value?: string | number
  checked?: boolean
  selected?: boolean
  indeterminate?: boolean
  name?: string
  type?: string
  label?: string
  'aria-label'?: string
}

declare module 'react' {
  namespace JSX {
    interface IntrinsicElements {
      'md-filled-button': MdElementProps
      'md-outlined-button': MdElementProps
      'md-text-button': MdElementProps
      'md-filled-tonal-button': MdElementProps
      'md-icon-button': MdElementProps
      'md-icon': MdElementProps
      'md-linear-progress': MdElementProps & { value?: number; indeterminate?: boolean }
      'md-circular-progress': MdElementProps & { value?: number; indeterminate?: boolean }
      'md-radio': MdElementProps
      'md-switch': MdElementProps
      'md-checkbox': MdElementProps
      'md-divider': MdElementProps
      'md-list': MdElementProps
      'md-list-item': MdElementProps
      'md-menu': MdElementProps
      'md-dialog': MdElementProps & { open?: boolean }
      'md-tabs': MdElementProps
      'md-primary-tab': MdElementProps
      'md-chip-set': MdElementProps
      'md-filter-chip': MdElementProps
      'md-assist-chip': MdElementProps
      'md-outlined-text-field': MdElementProps & { placeholder?: string; supportingText?: string }
    }
  }
}

export {}
