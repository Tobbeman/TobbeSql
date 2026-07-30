#!/bin/bash

START="$1"
END="$2"
BATCH="$3"

for ((i=START; i<=END; i+=BATCH)); do
  values=""
  for ((j=i; j<i+BATCH && j<=END; j++)); do
    [[ -n "$values" ]] && values+=", "
    values+="($j, 'test a very long long string to trigger new pages huraaaaaaaay$j')"
  done
  ./bin/Debug/net10.0/TobbeSQL "INSERT INTO BulkTable(id, name) VALUES $values"
done
