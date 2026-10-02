export function mapValues<T, V>(obj: T, valueMapper: (k: T[keyof T]) => V) {
   return Object.fromEntries(Object.entries(obj as any).map(([k, v]) => [k, valueMapper(v as T[keyof T])])) as { [K in keyof T]: V };
}
