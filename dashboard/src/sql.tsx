// Minimal SQL tokenizer for display highlighting. Not a parser — just enough
// to make keywords, strings, numbers, and parameters scannable.

const KEYWORDS = new Set([
  'select', 'from', 'where', 'insert', 'into', 'values', 'update', 'set',
  'delete', 'join', 'inner', 'left', 'right', 'outer', 'cross', 'on', 'as',
  'and', 'or', 'not', 'null', 'is', 'in', 'exists', 'between', 'like',
  'order', 'group', 'by', 'having', 'limit', 'offset', 'distinct', 'union',
  'all', 'case', 'when', 'then', 'else', 'end', 'create', 'table', 'index',
  'drop', 'alter', 'primary', 'key', 'foreign', 'references', 'constraint',
  'returning', 'with', 'asc', 'desc', 'top', 'count', 'sum', 'avg', 'min', 'max',
  'pragma', 'begin', 'commit', 'rollback', 'transaction', 'autoincrement',
]);

const TOKEN = /('(?:[^']|'')*')|(@\w+|\$\d+|\?)|(\b\d+(?:\.\d+)?\b)|(\b[a-zA-Z_]\w*\b)|(\s+|.)/g;

export function highlightSql(sql: string): React.ReactNode[] {
  const out: React.ReactNode[] = [];
  let m: RegExpExecArray | null;
  let i = 0;
  TOKEN.lastIndex = 0;
  while ((m = TOKEN.exec(sql)) !== null) {
    const [, str, param, num, word, rest] = m;
    if (str) out.push(<span key={i} className="sql-str">{str}</span>);
    else if (param) out.push(<span key={i} className="sql-param">{param}</span>);
    else if (num) out.push(<span key={i} className="sql-num">{num}</span>);
    else if (word) {
      out.push(
        KEYWORDS.has(word.toLowerCase())
          ? <span key={i} className="sql-kw">{word}</span>
          : word
      );
    } else out.push(rest);
    i++;
  }
  return out;
}
