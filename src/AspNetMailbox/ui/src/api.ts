declare global {
  interface Window { __MAILBOX_BASE__?: string; }
}

const raw = window.__MAILBOX_BASE__ ?? '';
export const basePath = raw.startsWith('%') ? '/_mailbox' : raw;
const apiBase = `${basePath}/api`;

export interface MailSummary {
  id: string;
  receivedAt: string;
  from: string;
  to: string[];
  subject: string;
  attachments: number;
  hasHtml: boolean;
}

export interface Attachment { index: number; fileName: string; contentType: string; size: number; }

export interface MailDetail {
  id: string;
  receivedAt: string;
  from: string;
  to: string[];
  cc: string[];
  bcc: string[];
  subject: string;
  htmlBody?: string | null;
  textBody?: string | null;
  headers: Record<string, string>;
  size: number;
  raw?: string | null;
  attachments: Attachment[];
}

async function getJson<T>(url: string, signal?: AbortSignal): Promise<T> {
  const res = await fetch(url, { signal });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
  return res.json();
}

export const api = {
  list: (search: string, page: number, signal?: AbortSignal) => {
    const p = new URLSearchParams({ page: String(page), pageSize: '50' });
    if (search) p.set('search', search);
    return getJson<{ items: MailSummary[]; totalCount: number; page: number; pageSize: number }>(
      `${apiBase}/messages?${p}`, signal);
  },
  get: (id: string) => getJson<MailDetail>(`${apiBase}/messages/${id}`),
  clear: () => fetch(`${apiBase}/clear`, { method: 'DELETE' }),
  attachmentUrl: (id: string, index: number) => `${apiBase}/messages/${id}/attachments/${index}`,
};
