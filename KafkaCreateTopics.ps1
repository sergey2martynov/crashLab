$topics = @("round.crashed", "bet.placed", "bet.cashed_out", "bet.settled", "dead-letter")

foreach ($topic in $topics) {
    docker compose exec kafka /opt/kafka/bin/kafka-topics.sh --create --if-not-exists --topic $topic --bootstrap-server localhost:9092 --partitions 3 --replication-factor 1
}

docker compose exec kafka /opt/kafka/bin/kafka-topics.sh --describe --bootstrap-server localhost:9092
