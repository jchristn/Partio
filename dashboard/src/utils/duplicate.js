// Helpers for the row "Duplicate" action: a duplicate opens the create form prefilled from an existing entry.

/** Name for a duplicated entry: "Name (copy)", "Name (copy 2)", and so on. Blank names stay blank. */
export function duplicateName(name) {
  const trimmed = (name || '').trim();
  if (!trimmed) return '';
  const match = trimmed.match(/^(.*) \(copy(?: (\d+))?\)$/);
  if (!match) return `${trimmed} (copy)`;
  const next = match[2] ? parseInt(match[2], 10) + 1 : 2;
  return `${match[1]} (copy ${next})`;
}
