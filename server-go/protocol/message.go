package protocol

type MessageType string

// const (
// 	// 클라이언트 -> 서버
// 	MsgPlayerJoin   MessageType = "player_join"
// 	MsgPlayerMove   MessageType = "player_move"
// 	MsgPlayerAction MessageType = "player_action"

// 	// 서버 -> 클라이언트
// 	MsgWorldState  MessageType = "world_state"
// 	MsgPlayerDied  MessageType = "player_died"
// 	MsgObjectSpawn MessageType = "object_spawn"
// )

// type Message struct {
// 	Type    MessageType `json:"type"`
// 	Payload interface{} `json:"payload"`
// }

// 플레이어 위치
type Position struct {
	X float64 `json:"x"`
	Y float64 `json:"y"`
	Z float64 `json:"z"`
}

// // 플레이어 이동 메시지
// type PlayerMovePayload struct {
// 	PlayerID string   `json:"player_id"`
// 	Position Position `json:"position"`
// 	Rotation float64  `json:"rotation"`
// }

// // 월드 상태 (주기적으로 전송)
// type WorldStatePayload struct {
// 	Players []PlayerState `json:"players"`
// 	Objects []GameObject  `json:"objects"`
// }

// type PlayerState struct {
// 	ID       string   `json:"id"`
// 	Position Position `json:"position"`
// 	Rotation float64  `json:"rotation"`
// 	IsAlive  bool     `json:"is_alive"`
// }

// type GameObject struct {
// 	ID       string   `json:"id"`
// 	Type     string   `json:"type"` // "stone", "obstacle" 등
// 	Position Position `json:"position"`
// }
