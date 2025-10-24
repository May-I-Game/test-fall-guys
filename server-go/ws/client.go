package ws

import (
	"time"

	"github.com/gorilla/websocket"
)

const (
	writeWait  = 10 * time.Second
	pongWait   = 60 * time.Second
	pingPeriod = (pongWait * 9) / 10
	// maxMessageSize = 512
)

type Client struct {
	ID   string
	Hub  *Hub
	Conn *websocket.Conn
	Send chan []byte
}

// 클라이언트로부터 메시지 읽기
func (c *Client) ReadPump() {
	defer func() {
		c.Hub.Unregister <- c
		c.Conn.Close()
	}()

	c.Conn.SetReadDeadline(time.Now().Add(pongWait))
	c.Conn.SetPongHandler(func(string) error {
		c.Conn.SetReadDeadline(time.Now().Add(pongWait))
		return nil
	})

	// for {
	// _, message, err := c.Conn.ReadMessage()
	// if err != nil {
	// 	if websocket.IsUnexpectedCloseError(err, websocket.CloseGoingAway, websocket.CloseAbnormalClosure) {
	// 		log.Printf("error: %v", err)
	// 	}
	// 	break
	// }

	// 메시지 파싱
	// var msg protocol.Message
	// if err := json.Unmarshal(message, &msg); err != nil {
	// 	log.Printf("error parsing message from %s: %v (raw: %s)", c.ID, err, string(message))

	// 	// JSON이 아닌 경우 에코 응답
	// 	response := map[string]interface{}{
	// 		"type": "echo",
	// 		"payload": map[string]string{
	// 			"original": string(message),
	// 			"from":     c.ID,
	// 		},
	// 	}
	// 	responseBytes, _ := json.Marshal(response)
	// 	c.Send <- responseBytes
	// 	continue
	// }

	// log.Printf("Client %s sent message type: %s", c.ID, msg.Type)

	// 메시지 타입에 따라 처리
	// switch msg.Type {
	// case protocol.MsgPlayerMove:
	// 	log.Printf("Player move received: %+v", msg.Payload)
	// 	// 모든 클라이언트에게 브로드캐스트
	// 	c.Hub.Broadcast <- message

	// case protocol.MsgPlayerJoin:
	// 	log.Printf("Player join received: %+v", msg.Payload)
	// 	// 환영 메시지 보내기
	// 	welcome := map[string]interface{}{
	// 		"type": "welcome",
	// 		"payload": map[string]string{
	// 			"message":   "Welcome to the game!",
	// 			"player_id": c.ID,
	// 		},
	// 	}
	// 	welcomeBytes, _ := json.Marshal(welcome)
	// 	c.Send <- welcomeBytes

	// 	// 다른 플레이어들에게도 알림
	// 	c.Hub.Broadcast <- message

	// default:
	// 	// 알 수 없는 메시지 타입은 에코
	// 	c.Hub.Broadcast <- message
	// }
	// 	}
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
