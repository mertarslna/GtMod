#!/bin/bash
# Growtopia proxy yönlendirmesini ve SSL sertifikasını temizler

if [ "$EUID" -ne 0 ]; then
  echo "Lütfen bu betiği sudo ile çalıştırın: sudo ./disable_proxy.sh"
  exit 1
fi

# 1. Hosts temizle
sed -i '' '/growtopia/d' /etc/hosts

# 2. SSL sertifikasını kaldır
security delete-certificate -c "www.growtopia1.com" /Library/Keychains/System.keychain 2>/dev/null

# 3. DNS temizle
dscacheutil -flushcache
killall -HUP mDNSResponder 2>/dev/null

echo "✅ Growtopia proxy yönlendirmesi kapatıldı ve orijinal haline döndü."
