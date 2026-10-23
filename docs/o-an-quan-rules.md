# Luật Ô ăn quan — biến thể Cheslax

Ô ăn quan có nhiều cách chơi theo vùng miền. Cheslax dùng **một bộ luật cố định**, triển khai trong `Cheslax.GameEngine`. Frontend dùng cùng luật trong `src/games/o-an-quan/oAnQuanEngine.ts`.

## Bàn cờ

- 12 ô xếp thành vòng: 10 ô dân (hai hàng, mỗi bên 5 ô) và 2 ô quan ở hai đầu.
- Chỉ số (theo chiều kim đồng hồ từ hàng dưới, trái → phải):

  | 11 ô quan trái | 10 9 8 7 6 (hàng trên, người chơi 2) |
  | --- | --- |
  | | 0 1 2 3 4 (hàng dưới, người chơi 1) | 5 ô quan phải |

- Khởi tạo: mỗi ô dân 5 quân dân; mỗi ô quan 1 quân quan.
- Điểm: dân = 1, quan = 5.

## Lượt đi

1. Người chơi 1 đi trước (hàng dưới).
2. Chọn một **ô dân của mình đang còn quân dân**. Không được chọn ô trống, ô đối phương, hay ô quan.
3. Chọn hướng rải: **thuận chiều kim đồng hồ** hoặc **ngược chiều**. Trên giao diện, nút trái/phải đã được đổi cho hàng trên để khớp hướng nhìn.
4. Nhặt toàn bộ quân dân trong ô đã chọn, rồi rải **từng quân một** vào các ô kế tiếp (kể cả ô quan).

## Rải tiếp và ăn quân

Sau khi rải hết tay:

- Nếu ô **kế tiếp là ô dân còn quân**: nhặt quân ô đó và rải tiếp cùng hướng (rải liên hoàn).
- Nếu ô **kế tiếp là ô quan còn quân**: **không** nhặt để rải — hết lượt.
- Nếu ô **kế tiếp trống** và ô ngay sau còn quân: **ăn** toàn bộ quân (dân và/hoặc quan) của ô đó. Ăn liên hoàn khi các ô xen kẽ trống / có quân theo cùng hướng.
- Các trường hợp khác: hết lượt.

## Tiếp quân

Khi đến lượt mà hàng dân của người đó trống:

- Lấy 5 điểm từ số đã ăn để đặt 1 dân vào mỗi ô dân của mình.
- Nếu thiếu dân, được đổi 1 quan = 5 dân.
- Không đủ 5 điểm thì **thua cuộc** (`cannot_resupply`).

## Kết thúc

- Khi **cả hai ô quan hết quân**, ván kết thúc. Mỗi bên cộng quân dân còn trên hàng của mình vào điểm đã ăn.
- Người nhiều điểm hơn thắng. Hòa nếu bằng điểm.
- Có thể đầu hàng; đối phương thắng.

## Ghi chú cho người mới

Chọn ô có quân trên hàng của bạn, rồi chọn trái hoặc phải. Quân sẽ được rải từng viên. Ăn khi gặp ô trống rồi tới ô có quân. Ô quan có quân thì chặn, không rải tiếp từ đó.
