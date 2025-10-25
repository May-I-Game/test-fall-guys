package ws

import (
	"log"
	"time"

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
		mt, message, err := c.Conn.ReadMessage()
		if err != nil {
			if websocket.IsUnexpectedCloseError(err, websocket.CloseGoingAway, websocket.CloseAbnormalClosure) {
				log.Printf("error: %v", err)
			}
			break
		}

		// 릴레이 서버 동작: 텍스트 메시지는 그대로 전체에게 브로드캐스트 (송신자 제외)
		if mt == websocket.TextMessage {
			// 디버그: 클라에서 들어온 원문 출력
			log.Printf("recv from %s: %s", c.ID, string(message))
			c.Hub.Broadcast <- BroadcastMsg{Sender: c, Data: message}
		} else {
			// 텍스트가 아니면 무시(필요시 이진도 중계하도록 변경 가능)
			log.Printf("ignored non-text message from %s (type=%d)", c.ID, mt)
		}
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

			// 디버그: 실제 소켓으로 전송된 메시지 로그
			log.Printf("send to %s: %s", c.ID, string(message))

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
