#!/bin/bash
# Growtopia proxy yönlendirmesini /etc/hosts ve SSL sertifikasını ayarlar

if [ "$EUID" -ne 0 ]; then
  echo "Lütfen bu betiği sudo ile çalıştırın: sudo ./enable_proxy.sh"
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# 1. SSL Sertifikasını macOS Sistemine Güvenilir Olarak Ekle (HTTPS 443 için)
if [ -f "$SCRIPT_DIR/server.crt" ]; then
  security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain "$SCRIPT_DIR/server.crt" 2>/dev/null
  echo "🔒 SSL Sertifikası sisteme eklendi."
fi

# 2. Hosts dosyasını güncelle
sed -i '' '/growtopia/d' /etc/hosts

cat << 'EOF' >> /etc/hosts
127.0.0.1 www.growtopia1.com
127.0.0.1 www.growtopia2.com
127.0.0.1 growtopia1.com
127.0.0.1 growtopia2.com
EOF

# 3. macOS DNS önbelleğini temizle
dscacheutil -flushcache
killall -HUP mDNSResponder 2>/dev/null

echo "✅ Growtopia proxy ve SSL yönlendirmesi başarıyla açıldı!"
echo "Şimdi 'sudo dotnet run' ile proxy'yi başlatıp oyuna girebilirsiniz."
