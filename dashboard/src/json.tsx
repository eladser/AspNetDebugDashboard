import { useState } from 'react';

type JVal = null | boolean | number | string | JVal[] | { [k: string]: JVal };

function JsonNode({ value, depth }: { value: JVal; depth: number }) {
  const [open, setOpen] = useState(depth < 2);

  if (value === null) return <span className="jv-null">null</span>;
  if (typeof value === 'boolean') return <span className="jv-bool">{String(value)}</span>;
  if (typeof value === 'number') return <span className="jv-num">{value}</span>;
  if (typeof value === 'string') return <span className="jv-str">"{value}"</span>;

  const isArray = Array.isArray(value);
  const entries: [string, JVal][] = isArray
    ? value.map((v, i) => [String(i), v])
    : Object.entries(value);
  const brackets = isArray ? ['[', ']'] : ['{', '}'];

  if (entries.length === 0) return <span className="jv-punc">{brackets[0]}{brackets[1]}</span>;

  return (
    <span>
      <button className="jv-toggle" onClick={() => setOpen((o) => !o)}>{open ? '▾' : '▸'}</button>
      <span className="jv-punc">{brackets[0]}</span>
      {!open && <span className="jv-collapsed">{entries.length} {isArray ? 'items' : 'keys'}</span>}
      {open && (
        <div className="jv-indent">
          {entries.map(([k, v], i) => (
            <div className="jv-row" key={k}>
              {!isArray && <span className="jv-key">{k}</span>}
              {!isArray && <span className="jv-punc">: </span>}
              <JsonNode value={v} depth={depth + 1} />
              {i < entries.length - 1 && <span className="jv-punc">,</span>}
            </div>
          ))}
        </div>
      )}
      <span className="jv-punc">{brackets[1]}</span>
    </span>
  );
}

export function JsonTree({ json }: { json: string }) {
  let parsed: JVal;
  try {
    parsed = JSON.parse(json);
  } catch {
    return <pre className="code">{json}</pre>;
  }
  return (
    <div className="json-tree">
      <JsonNode value={parsed} depth={0} />
    </div>
  );
}
