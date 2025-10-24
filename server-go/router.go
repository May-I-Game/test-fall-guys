package main

import (
	"net/http"
	"server-go/ws"
)

func NewRouter() *http.ServeMux {
	r := http.NewServeMux()
	r.HandleFunc("/ws", ws.HandleEcho)
	return r
}
