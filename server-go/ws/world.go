package ws

import (
	"encoding/json"
	"server-go/protocol"
	"sync"
)

// World는 접속한 각 클라이언트의 최신 좌표를 보관합니다.
type World struct {
	mu      sync.RWMutex
	players map[string]protocol.Position
}

func NewWorld() *World {
	return &World{players: make(map[string]protocol.Position)}
}

func (w *World) UpdatePlayerPos(id string, pos protocol.Position) {
	w.mu.Lock()
	w.players[id] = pos
	w.mu.Unlock()
}

func (w *World) RemovePlayer(id string) {
	w.mu.Lock()
	delete(w.players, id)
	w.mu.Unlock()
}

type SnapshotPlayer struct {
	ID string  `json:"id"`
	X  float64 `json:"x"`
	Y  float64 `json:"y"`
	Z  float64 `json:"z"`
}

// Cube 포맷: Unity에서 사용하는 배치 포맷과 동일하게 맞춥니다.
type Cube struct {
	ID    string  `json:"id"`
	Color string  `json:"color"`
	X     float64 `json:"x"`
	Y     float64 `json:"y"`
	Z     float64 `json:"z"`
	QX    float64 `json:"qx"`
	QY    float64 `json:"qy"`
	QZ    float64 `json:"qz"`
	QW    float64 `json:"qw"`
}

type CubeBatch struct {
	Type  string `json:"type"`
	Cubes []Cube `json:"cubes"`
}

// Snapshot은 현재 저장된 좌표만 기반으로 큐브 포맷을 생성합니다.
// 회전/색상은 아직 저장하지 않으므로 기본값으로 채웁니다.
func (w *World) Snapshot() CubeBatch {
	w.mu.RLock()
	defer w.mu.RUnlock()
	out := CubeBatch{Type: "cubes", Cubes: make([]Cube, 0, len(w.players))}
	for id, p := range w.players {
		out.Cubes = append(out.Cubes, Cube{
			ID:    id,
			Color: "#FFFFFF",
			X:     p.X,
			Y:     p.Y,
			Z:     p.Z,
			QX:    0,
			QY:    0,
			QZ:    0,
			QW:    1,
		})
	}
	return out
}

func (w *World) SnapshotJSON() []byte {
	s := w.Snapshot()
	b, _ := json.Marshal(s)
	return b
}
