            foreach (var header in context.Request.Headers)
            {
                if (!_config.ExcludedHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase))
                {
                    headers[header.Key] = string.Join(", ", header.Value.ToArray());
                }
            }