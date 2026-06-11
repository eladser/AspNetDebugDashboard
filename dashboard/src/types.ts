// Mirrors the C# models in Core/Models. ASP.NET Core serializes camelCase.

interface EntryBase {
  id: string;
  requestId: string;
  timestamp: string;
  type: string;
  correlationId?: string | null;
  userId?: string | null;
  sessionId?: string | null;
  metadata: Record<string, unknown>;
}

export interface RequestEntry extends EntryBase {
  method: string;
  url: string;
  path: string;
  queryString: string;
  statusCode: number;
  executionTimeMs: number;
  headers: Record<string, string>;
  requestBody?: string | null;
  responseBody?: string | null;
  contentType?: string | null;
  responseContentType?: string | null;
  userAgent?: string | null;
  ipAddress?: string | null;
  sqlQueries: SqlQueryEntry[];
  logs: LogEntry[];
  exception?: string | null;
  requestSize: number;
  responseSize: number;
  protocol?: string | null;
  isHttps: boolean;
}

export interface SqlQueryEntry extends EntryBase {
  query: string;
  parameters: Record<string, unknown>;
  executionTimeMs: number;
  rowsAffected: number;
  database?: string | null;
  isSuccessful: boolean;
  error?: string | null;
  commandType?: string | null;
  isSlowQuery: boolean;
  stackTrace?: string | null;
}

export interface LogEntry extends EntryBase {
  level: string;
  message: string;
  category?: string | null;
  tag?: string | null;
  properties: Record<string, unknown>;
  stackTrace?: string | null;
  exception?: string | null;
  threadId?: number | null;
  machineName?: string | null;
}

export interface ExceptionEntry extends EntryBase {
  message: string;
  stackTrace?: string | null;
  source?: string | null;
  route?: string | null;
  method?: string | null;
  path?: string | null;
  exceptionType?: string | null;
  innerException?: ExceptionEntry | null;
  data: Record<string, unknown>;
  userAgent?: string | null;
  ipAddress?: string | null;
  requestBody?: string | null;
  headers: Record<string, string>;
  targetSite?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface SlowRequest {
  id: string;
  method: string;
  path: string;
  executionTimeMs: number;
  timestamp: string;
}

export interface SlowQuery {
  id: string;
  query: string;
  executionTimeMs: number;
  timestamp: string;
}

export interface DebugStats {
  totalRequests: number;
  totalSqlQueries: number;
  totalExceptions: number;
  totalLogs: number;
  averageResponseTime: number;
  averageSqlTime: number;
  statusCodeDistribution: Record<string, number>;
  requestMethodDistribution: Record<string, number>;
  exceptionTypeDistribution: Record<string, number>;
  slowestRequests: SlowRequest[];
  slowestQueries: SlowQuery[];
  lastUpdated: string;
}

export interface ListQuery {
  page: number;
  pageSize: number;
  search?: string;
  sortBy?: string;
  sortDescending?: boolean;
  method?: string;
  statusCode?: number;
  level?: string;
  isSuccessful?: boolean;
  isSlowQuery?: boolean;
  minExecutionTime?: number;
  requestId?: string;
}

export interface EndpointPerf {
  endpoint: string;
  averageTime: number;
  requestCount: number;
}

export interface PerfMetrics {
  totalRequests: number;
  averageResponseTime: number;
  medianResponseTime: number;
  p95ResponseTime: number;
  p99ResponseTime: number;
  errorRate: number;
  requestsPerMinute: number;
  slowestEndpoints: EndpointPerf[];
  statusCodeDistribution: { statusCode: number; count: number; percentage: number }[];
}

export interface SearchHit {
  type: 'request' | 'query' | 'log' | 'exception';
  data: Record<string, unknown>;
}
