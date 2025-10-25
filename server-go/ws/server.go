package ws

import (
	"fmt"
	"log"
	"net/http"
	"time"
)

func ServeWs(hub *Hub, world *World, w http.ResponseWriter, r *http.Request) {
	conn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		log.Println(err)
		return
	}

	client := &Client{
		ID:    generateClientID(),
		Hub:   hub,
		Conn:  conn,
		Send:  make(chan []byte, 256),
		World: world,
	}

	client.Hub.Register <- client

	log.Printf("New client connected: %s", client.ID)

	// 고루틴으로 읽기/쓰기 시작
	go client.WritePump()
	go client.ReadPump()
}

func generateClientID() string {
	// 간단한 ID 생성 - 실제로는 UUID 사용 권장
	return fmt.Sprintf("player_%d", time.Now().UnixNano())
}
