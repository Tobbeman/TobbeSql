#!/bin/bash

START=100001
END=1000000
BATCH=1000

for ((i=START; i<=END; i+=BATCH)); do
  values=""
  for ((j=i; j<i+BATCH && j<=END; j++)); do
    [[ -n "$values" ]] && values+=", "
    values+="($j, 'test$j')"
  done
  ./bin/Debug/net10.0/TobbeSQL "INSERT INTO MyTestTable(id, name) VALUES $values"
done
