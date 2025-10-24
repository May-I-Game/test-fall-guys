package main

import (
	"log"
	"net/http"

	"server-go/ws"
)

func main() {
	// Hub 생성 및 실행
	hub := ws.NewHub()
	go hub.Run()

	// WebSocket 엔드포인트
	http.HandleFunc("/ws", func(w http.ResponseWriter, r *http.Request) {
		ws.ServeWs(hub, w, r)
	})

	// 서버 시작
	log.Println("Game server starting on :8080")
	log.Println("WebSocket endpoint: ws://localhost:8080/ws")

	err := http.ListenAndServe(":8080", nil)
	if err != nil {
		log.Fatal("ListenAndServe error: ", err)
	}
}
