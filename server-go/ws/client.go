package ws

import (
	"encoding/json"
	"log"
	"time"

	"server-go/protocol"

	"github.com/gorilla/websocket"
)

const (
	writeWait  = 10 * time.Second
	pongWait   = 60 * time.Second
	pingPeriod = (pongWait * 9) / 10
)

type Client struct {
	ID    string
	Hub   *Hub
	Conn  *websocket.Conn
	Send  chan []byte
	World *World
}

// 클라이언트로부터 메시지 읽기
func (c *Client) ReadPump() {
	defer func() {
		// 월드에서 제거 후 허브 해제
		if c.World != nil {
			c.World.RemovePlayer(c.ID)
		}
		c.Hub.Unregister <- c
		c.Conn.Close()
	}()

	c.Conn.SetReadDeadline(time.Now().Add(pongWait))
	c.Conn.SetPongHandler(func(string) error {
		c.Conn.SetReadDeadline(time.Now().Add(pongWait))
		return nil
	})

	for {
		_, message, err := c.Conn.ReadMessage()
		if err != nil {
			if websocket.IsUnexpectedCloseError(err, websocket.CloseGoingAway, websocket.CloseAbnormalClosure) {
				log.Printf("error: %v", err)
			}
			break
		}

		// {"x":...,"y":...,"z":...} 포맷만 처리 (필드가 모두 존재하는지 검사)
		var pin struct {
			X *float64 `json:"x"`
			Y *float64 `json:"y"`
			Z *float64 `json:"z"`
		}
		if err := json.Unmarshal(message, &pin); err == nil && pin.X != nil && pin.Y != nil && pin.Z != nil {
			pos := protocol.Position{X: *pin.X, Y: *pin.Y, Z: *pin.Z}
			log.Printf("pos from %s => x=%.3f y=%.3f z=%.3f", c.ID, pos.X, pos.Y, pos.Z)
			// 최소 브로드캐스트: 받은 좌표 JSON을 그대로 전체에게 전송
			c.Hub.Broadcast <- message
			continue
		}

		// 위치가 아니면 간단히 로깅
		log.Printf("non-position message from %s: %s", c.ID, string(message))
	}
}

// 클라이언트에게 메시지 쓰기
func (c *Client) WritePump() {
	ticker := time.NewTicker(pingPeriod)
	defer func() {
		ticker.Stop()
		c.Conn.Close()
	}()

	for {
		select {
		case message, ok := <-c.Send:
			c.Conn.SetWriteDeadline(time.Now().Add(writeWait))
			if !ok {
				c.Conn.WriteMessage(websocket.CloseMessage, []byte{})
				return
			}

			w, err := c.Conn.NextWriter(websocket.TextMessage)
			if err != nil {
				return
			}
			w.Write(message)

			if err := w.Close(); err != nil {
				return
			}

		case <-ticker.C:
			c.Conn.SetWriteDeadline(time.Now().Add(writeWait))
			if err := c.Conn.WriteMessage(websocket.PingMessage, nil); err != nil {
				return
			}
		}
	}
}
