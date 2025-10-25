package ws

import "log"

type Hub struct {
	Clients    map[*Client]bool
	Broadcast  chan BroadcastMsg
	Register   chan *Client
	Unregister chan *Client
}

// BroadcastMsg는 특정 송신자(Sender)를 제외하고 다른 모든 클라이언트에게
// 전달할 메시지 페이로드(Data)를 담습니다.
type BroadcastMsg struct {
	Sender *Client
	Data   []byte
}

func NewHub() *Hub {
	return &Hub{
		Broadcast:  make(chan BroadcastMsg),
		Register:   make(chan *Client),
		Unregister: make(chan *Client),
		Clients:    make(map[*Client]bool),
	}
}

func (h *Hub) Run() {
	for {
		select {
		case client := <-h.Register:
			h.Clients[client] = true

		case client := <-h.Unregister:
			if _, ok := h.Clients[client]; ok {
				delete(h.Clients, client)
				close(client.Send)
			}

		case bm := <-h.Broadcast:
			// 디버그: 서버에서 나가는 브로드캐스트 로그 (송신자 제외, 대상 수 포함)
			targets := len(h.Clients)
			if bm.Sender != nil {
				targets--
			}
			if targets < 0 {
				targets = 0
			}
			log.Printf("broadcasting to %d clients (others-only): %s", targets, string(bm.Data))
			for client := range h.Clients {
				if bm.Sender != nil && client == bm.Sender {
					continue // 송신자 제외
				}
				select {
				case client.Send <- bm.Data:
				default:
					close(client.Send)
					delete(h.Clients, client)
				}
			}
		}
	}
}
