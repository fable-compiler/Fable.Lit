module ImportSideEffect

let mutable private value = 0

let getValue() = value
let setValue(v) = value <- v